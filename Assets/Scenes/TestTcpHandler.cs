using FlatBuffers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestTcpHandler : ITcpClientHandler
{
    public void OnClose(bool silently)
    {
        Debug.Log("TestTcpHandler.OnClose");
    }

    public void OnConnect()
    {
        Debug.Log("TestTcpHandler.OnConnect");
    }

    public bool OnPacket(ByteBuffer buffer, out int packetCode)
    {
        packetCode = 0;
        return false;
    }
}