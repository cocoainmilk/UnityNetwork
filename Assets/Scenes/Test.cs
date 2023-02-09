
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class Test : MonoBehaviour
{
    void Start()
    {
        TestWebClent();
        TestTcpClient();
    }

    void TestWebClent()
    {
        WebClient client = new("naver.com");
        client.Request(new TestWebProtocol())
            .DoOnError(ex => Debug.LogError(ex))
            .Subscribe(_ => Debug.Log("TestWebClent success"))
            .AddTo(this);
    }

    enum ResultCode
    {
        ResultCode
    }

    void TestTcpClient()
    {
        TcpClient client = new("naver.com", 80, new TestTcpHandler(), 1024);
        client.Connect(30)
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
