using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UniRx;
using WebProtocol;

public class TrustCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}

public class WebClient
{
    string baseUrl;

    bool requesting = false; // 동시 요청을 막기 위해

    public WebClient(string baseUrl)
    {
        this.baseUrl = baseUrl;
        UnityWebRequest.ClearCookieCache();
    }

    public IObservable<T> Request<T>(T protocol) where T : ProtocolBase
    {
        return Observable.FromCoroutine<T>(observer => RequestInternal(protocol, observer))
            .Finally(() =>
            {
                requesting = false;
            });
    }

    IEnumerator RequestInternal<T>(T protocol, IObserver<T> observer) where T : ProtocolBase
    {
        while(requesting)
        {
            yield return null;
        }

        requesting = true;

        string url = protocol.GetUrl(baseUrl);
        MethodAttribute method = protocol.GetMethod();
        string body = null;

        // 요청 content를 만듬
        UnityWebRequest request;
        switch(method.Method)
        {
            case Method.Get:
                request = UnityWebRequest.Get(url);
                Debug.Log($"WebClient GET : {url}");
                break;
            case Method.Post:
                ContentType type = protocol.GetContentType();
                if(type == ContentType.None)
                {
                    request = UnityWebRequest.Post(url, string.Empty);
                    Debug.Log($"WebClient POST : {url}");
                }
                else if(type == ContentType.Form)
                {
                    request = UnityWebRequest.Post(url, protocol.GetBodyForm());
                    Debug.Log($"WebClient POST : {url} {request.uploadHandler.contentType}");
                }
                else
                {
                    body = protocol.GetBodyJSON();
                    request = new UnityWebRequest(url);
                    request.method = UnityWebRequest.kHttpVerbPOST;
                    if(!string.IsNullOrEmpty(body))
                    {
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                        request.uploadHandler.contentType = "application/json";
                    }
                    request.downloadHandler = new DownloadHandlerBuffer();
                    Debug.Log($"WebClient POST : {url} {body}");
                }
                break;
            case Method.Delete:
                request = UnityWebRequest.Delete(url);
                Debug.Log($"WebClient DELETE : {url}");
                break;
            default:
                throw new ArgumentException("No supported method.");
        }

        using(request)
        {
            request.certificateHandler = new TrustCertificateHandler();
#if !UNITY_EDITOR
            request.timeout = 30;
#endif
            yield return request.SendWebRequest();

            // 응답 에러 처리

            if(request.result == UnityWebRequest.Result.ConnectionError)
            {
                string message = $"Request ConnectionError({request.error}). {url}, {body}";
                throw new ConnectionException(message);
            }

            if(request.result == UnityWebRequest.Result.DataProcessingError)
            {
                string message = $"Request DataProcessingError({request.error}). {url}, {body}";
                throw new DataProcessingException(message);
            }

            if(request.result == UnityWebRequest.Result.ProtocolError)
            {
                string message = $"Request ProtocolError({request.responseCode}). {url}, {body}";
                throw new ProtocolException(message, request.responseCode, body);
            }

            // 정상 응답

            string responseBody = string.Empty;
            var data = request.downloadHandler.data;
            if(data != null && data.Length > 0)
            {
                responseBody = Encoding.UTF8.GetString(data);
            }

            Debug.Log($"Response : {url}, {body} -> {responseBody}");
            if(responseBody != string.Empty)
            {
                protocol.SetResponse(responseBody);
            }
        }

        observer.OnNext(protocol);
        observer.OnCompleted();
    }
}


