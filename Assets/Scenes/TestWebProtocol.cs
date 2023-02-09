using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebProtocol;

[Method(Method.Get)]
public class TestWebProtocol : ProtocolBase
{
    [System.Serializable]
    public struct RequestBody
    {
    }

    [System.Serializable]
    public struct ResponseBody
    {
    }

    [Path] public const string Path = "index.html";
    [RequestBody] public RequestBody Request;
    [ResponseBody] public ResponseBody Response;
}