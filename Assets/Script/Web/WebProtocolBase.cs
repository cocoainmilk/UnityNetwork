using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace WebProtocol
{
    public enum Method
    {
        Get,
        Post,
        Delete,
    }

    public enum ContentType
    {
        None,
        Json,
        Form,
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class MethodAttribute : Attribute
    {
        public Method Method { get; set; }
        public MethodAttribute(Method method)
        {
            this.Method = method;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class PathAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class QueryAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RequestBodyAttribute : Attribute
    {
        public ContentType ContentType { get; set; }
        public RequestBodyAttribute(ContentType ContentType = ContentType.Json)
        {
            this.ContentType = ContentType;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ResponseBodyAttribute : Attribute
    {
    }

    public class ProtocolBase
    {
        public MethodAttribute GetMethod()
        {
            MethodAttribute[] attrs = (MethodAttribute[])GetType().GetCustomAttributes(typeof(MethodAttribute), false);
            if(attrs.Length != 1)
            {
                throw new System.ArgumentException("No MethodAttribute in protocol.");
            }

            return attrs[0];
        }

        public ContentType GetContentType()
        {
            foreach(FieldInfo field in GetType().GetFields())
            {
                if(field.IsDefined(typeof(RequestBodyAttribute), false))
                {
                    return field.GetCustomAttribute<RequestBodyAttribute>().ContentType;
                }
            }

            return ContentType.None;
        }

        public string GetUrl(string baseURL)
        {
            StringBuilder url = new StringBuilder(baseURL, 128);
            Type protocolType = GetType();

            // Path
            foreach(FieldInfo field in protocolType.GetFields())
            {
                if(field.IsDefined(typeof(PathAttribute), false))
                {
                    url.Append('/');
                    url.Append(field.GetValue(this).ToString());
                }
            }

            // Query
            url.Append('?');
            foreach(FieldInfo field in protocolType.GetFields())
            {
                if(field.IsDefined(typeof(QueryAttribute), false))
                {
                    url.Append(field.Name);
                    url.Append('=');
                    url.Append(UnityWebRequest.EscapeURL(field.GetValue(this).ToString()));
                    url.Append('&');
                }
            }
            char last = url[url.Length - 1];
            if(last == '?' || last == '&')
            {
                url.Remove(url.Length - 1, 1);
            }

            return url.ToString();
        }

        public string GetBodyJSON()
        {
            Type protocolType = GetType();
            foreach(FieldInfo field in protocolType.GetFields())
            {
                if(field.IsDefined(typeof(RequestBodyAttribute), false))
                {
                    return JsonUtility.ToJson(field.GetValue(this));
                }
            }
            return null;
        }

        public WWWForm GetBodyForm()
        {
            Type protocolType = GetType();
            foreach(FieldInfo field in protocolType.GetFields())
            {
                if(field.IsDefined(typeof(RequestBodyAttribute), false))
                {
                    return (WWWForm)field.GetValue(this);
                }
            }
            return null;
        }

        // 응답은 JSON으로 되어 있다고 가정됨
        public bool SetResponse(string response)
        {
            FieldInfo fieldInfo = null;
            foreach(FieldInfo field in GetType().GetFields())
            {
                if(field.IsDefined(typeof(ResponseBodyAttribute), false))
                {
                    fieldInfo = field;
                    break;
                }
            }


            if(fieldInfo != null)
            {
                // array 하나만 있는 경우 ResponseBody 선언에 문제가 있을 수 있어서 예외처리
                if(response.Length > 0 && response[0] == '[')
                {
                    StringBuilder builder = new StringBuilder();
                    builder.Append("{\"array\":");
                    builder.Append(response);
                    builder.Append("}");
                    response = builder.ToString();
                }

                object value = JsonUtility.FromJson(response, fieldInfo.FieldType);
                fieldInfo.SetValue(this, value);
            }

            return true;
        }
    }
}
