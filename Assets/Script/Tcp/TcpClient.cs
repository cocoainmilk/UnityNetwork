using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using FlatBuffers;

public class TcpClient
{
    const int SizePrefixLength = 4;

    string host;
    int port;

    System.Net.Sockets.TcpClient connectedClient;

    byte[] bufferRead;
    int bufferReadOffset;
    int bufferReadRemainingSize;

    FlatBufferBuilder bufferWrite;

    Dictionary<int/*PacketCode*/, Subject<ByteBuffer>> waitingResponse = new();

    ITcpClientHandler handler;

    public TcpClient(string host, int port, ITcpClientHandler handler, int maxBufferSize)
    {
        this.host = host;
        this.port = port;
        this.handler = handler;

        bufferRead = new byte[maxBufferSize];
        bufferWrite = new FlatBufferBuilder(maxBufferSize);
    }

    public FlatBufferBuilder GetBufferWrite()
    {
        bufferWrite.Clear();
        return bufferWrite;
    }

    public void Close(bool silently = false)
    {
        if(connectedClient != null)
        {
            Debug.Log($"TcpClient.Close: {host}:{port}");
            connectedClient.Close();
            connectedClient = null;

            handler?.OnClose(silently);
        }

        CancelWaitingResponseAll();
    }

    void CloseOtherThread()
    {
        if(MainThreadDispatcher.IsInitialized)
        {
            MainThreadDispatcher.Post((_) => Close(), null);
        }
    }

    public IObservable<Unit> Connect(float timeoutSec)
    {
        return Observable.Create<Unit>(observer =>
        {
            var client = new System.Net.Sockets.TcpClient(System.Net.Sockets.AddressFamily.InterNetworkV6);
            client.NoDelay = true;

            var observableTimeout = Observable.Timer(TimeSpan.FromSeconds(timeoutSec)).Select(_ => true);
            var observableConnect = Observable.Defer(
                    Observable.FromAsyncPattern((callback, state) =>
                    {
                        Debug.Log($"TcpClient.Connect: {host}:{port} connecting...");
                        return client.BeginConnect(host, port, callback, client);
                    },
                    iar =>
                    {
                        client.EndConnect(iar);
                    })
                )
                .Select(_ => false);

            Observable.Merge(observableTimeout, observableConnect)
                .Take(1)
                .ObserveOnMainThread()
                .Subscribe(timeout =>
                {
                    if(timeout)
                    {
                        observer.OnError(new Exception(string.Format("TcpClient.Connect: {0}:{1} timeout", host, port)));
                    }
                    else
                    {
                        Close(true);

                        Debug.LogFormat("TcpClient.Connect: {0}:{1} connected", host, port);

                        connectedClient = client;

                        // 패킷의 앞부분에 패킷크기 정보가 있다고 가정되어 있어 그 부분을 우선 읽는다.
                        bufferReadOffset = 0;
                        bufferReadRemainingSize = SizePrefixLength;

                        try
                        {
                            var stream = connectedClient.GetStream();
                            stream.BeginRead(bufferRead, bufferReadOffset, bufferReadRemainingSize, OnReadOtherThread, stream);
                        }
                        catch(Exception ex)
                        {
                            observer.OnError(ex);
                            return;
                        }

                        observer.OnNext(Unit.Default);
                        observer.OnCompleted();
                    }
                },
                ex => observer.OnError(ex));

            return Disposable.Empty;
        })
        .Do(_ =>
        {
            handler?.OnConnect();
        })
        .DoOnError(ex => Debug.LogException(ex));
    }

    public bool IsConnected()
    {
        return connectedClient != null && connectedClient.Connected;
    }

    void OnReadOtherThread(IAsyncResult asyncResult)
    {
        try
        {
            var stream = (System.Net.Sockets.NetworkStream)asyncResult.AsyncState;

            int readBytes = stream.EndRead(asyncResult);
            if(readBytes == 0)
            {
                Debug.LogError("TcpClient.OnReadMainThread: readBytes is zero");
                CloseOtherThread();
                return;
            }

            bufferReadOffset += readBytes;
            bufferReadRemainingSize -= readBytes;
            if(bufferReadRemainingSize == 0)
            {
                if(bufferReadOffset == SizePrefixLength)
                {
                    // 패킷크기 값만큼의 바디를 읽는다.
                    bufferReadOffset = 0;
                    bufferReadRemainingSize = new ByteBuffer(bufferRead).GetInt(0);

                    if(bufferReadRemainingSize > bufferRead.Length)
                    {
                        throw new Exception($"TcpClient.OnReadMainThread: buffer overflow {bufferReadRemainingSize}");
                    }
                }
                else
                {
                    // 패킷바디 읽기 완료.
                    var bufferCopy = new byte[bufferReadOffset];
                    Buffer.BlockCopy(bufferRead, 0, bufferCopy, 0, bufferCopy.Length);
                    var buffer = new ByteBuffer(bufferCopy);
                    MainThreadDispatcher.Post(OnReadMainThread, buffer);
                    bufferReadOffset = 0;
                    bufferReadRemainingSize = FlatBufferConstants.SizePrefixLength;
                }
            }

            stream.BeginRead(bufferRead, bufferReadOffset, bufferReadRemainingSize, OnReadOtherThread, stream);
        }
        catch(ObjectDisposedException)
        {
            CloseOtherThread();
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
            CloseOtherThread();
        }
    }

    void OnReadMainThread(object state)
    {
        var buffer = (ByteBuffer)state;

        try
        {
            // 패킷 응답 처리. 핸들러에 맡기거나 응답용 suject에 맡긴다.
            if(!handler.OnPacket(buffer, out var packetCode))
            {
                if(waitingResponse.TryGetValue(packetCode, out var subject))
                {
                    waitingResponse.Remove(packetCode);
                    subject.OnNext(buffer);
                    subject.OnCompleted();
                }
                else
                {
                    Debug.LogError($"TcpClient.OnReadMainThread: invalid packet code {packetCode}");
                }
            }
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    // 패킷 쓰기. 응답은 핸들러에게 맡김
    public void Write(FlatBufferBuilder builder)
    {
        if(!IsConnected())
        {
            return;
        }

        try
        {
            var stream = connectedClient.GetStream();

            var size = builder.DataBuffer.Length - builder.DataBuffer.Position;
            builder.PutInt(size);
            builder.DataBuffer.Position -= SizePrefixLength;

            var buffer = builder.SizedByteArray();
            stream.BeginWrite(buffer, 0, buffer.Length, OnWriteOtherThread, stream);
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
            Close();
        }
    }

    // 패킷 쓰기. 응답은 observable을 통해 처리
    public IObservable<ByteBuffer> Write(FlatBufferBuilder builder, Enum resultCode)
    {
        return Write(builder, Convert.ToInt32(resultCode));
    }

    IObservable<ByteBuffer> Write(FlatBufferBuilder builder, int resultCode)
    {
        var observable = Observable.Defer(() =>
        {
            if(!IsConnected())
            {
                throw new Exception($"TcpClient.Write: {host}:{port} disconnected");
            }

            try
            {
                var stream = connectedClient.GetStream();
                Subject<ByteBuffer> subject = new Subject<ByteBuffer>();
                if(waitingResponse.TryGetValue(resultCode, out var exists))
                {
                    exists.OnError(new Exception($"TcpClient.Write: {host}:{port} duplicated code {resultCode}"));
                    waitingResponse.Remove(resultCode);
                }
                waitingResponse.Add(resultCode, subject);

                Write(builder);


                return subject;
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
                Close();
                throw ex;
            }
        });

        return observable;
    }

    void OnWriteOtherThread(IAsyncResult asyncResult)
    {
        try
        {
            var stream = (System.Net.Sockets.NetworkStream)asyncResult.AsyncState;
            stream.EndWrite(asyncResult);
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
            CloseOtherThread();
        }
    }

    public void RemoveWaitingResponse(Enum resultCode)
    {
        var code = Convert.ToInt32(resultCode);
        waitingResponse.Remove(code);
    }

    public void CancelWaitingResponseAll()
    {
        if(waitingResponse.Count > 0)
        {
            try
            {
                foreach(var subject in waitingResponse.Values)
                {
                    subject.Dispose();
                }
            }
            catch(Exception)
            {

            }

            waitingResponse.Clear();
        }
    }
}
