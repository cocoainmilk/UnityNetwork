using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlatBuffers;

public interface ITcpClientHandler
{
    void OnConnect();
    bool OnPacket(ByteBuffer buffer, out int packetCode);
    void OnClose(bool silently);
}
