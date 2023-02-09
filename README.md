# UnityNetwork

## WebClient
- 요청의 메소드는 GET, POST, DELETE를 지원하고 파라미터는 query, json, form를 지원
- 응답은 JSON만 지원
- 프로토콜은 ProtocolBase을 상속하여 만들 수 있다. (TestWebProtocol 참고)

## TcpClient
- System.Net.Sockets.TcpClient와 FlatBuffer를 사용한 TcpClient
- 응답은 handler나 observable를 통해 처리
