
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
         * ����. ������ �ڵ鷯�� ���� ó��
         * client.Write(builders);
         * 
         * ����. ������ observable�� ���� ó��
         * client.Write(builders, ResultCode)
         *      .Subscribe(response => Debug.Log("response"))
         *      .AddTo(this);
        */
    }
}
