
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class Test : MonoBehaviour
{
    WebClient webClient;
    TcpClient tcpClient;

    void Start()
    {
        TestWebClent();
        TestTcpClient();
    }

    void TestWebClent()
    {
        webClient = new("naver.com");
        webClient.Request(new TestWebProtocol())
            .DoOnError(ex => Debug.LogError(ex))
            .Subscribe(_ => Debug.Log("TestWebClent success"))
            .AddTo(this);
    }

    void TestTcpClient()
    {
        tcpClient = new("naver.com", 80, new TestTcpHandler(), 1024);
        tcpClient.Connect(30)
            .Subscribe(_ => Debug.Log("TestTcpClient connected"))
            .AddTo(this);

        /*
         * FlatBufferBuilder builders;
         * 
         * 쓰기. 응답은 핸들러를 통해 처리
         * client.Write(builders);
         * 
         * 쓰기. 응답은 observable를 통해 처리
         * client.Write(builders, ResultCode)
         *      .Subscribe(response => Debug.Log("response"))
         *      .AddTo(this);
        */
    }
}
