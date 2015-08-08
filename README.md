# NAT_Test
Application 입장에서 자신이 속한 NAT에 대해 다음 항목들을 조사한다.
 - Address and Port Mapping Behavior ([1][글1],[2](https://tools.ietf.org/html/rfc4787#section-4.1))
 - Filtering Behavior ([1][글2],[2](https://tools.ietf.org/html/rfc4787#section-5))
 - Hairpin 지원여부 ([1][글2],[2](https://tools.ietf.org/html/rfc4787#section-6))


## 테스트 시 주의사항
 - Client, MainServer, SubServer는 각기 다른 호스트에서 실행해야 한다.
 - MainServer와 SubServer는 테스트 대상 NAT의 바깥에서 실행해야 한다.
 - MainServer와 SubServer 그리고 테스트 대상 NAT는 완전한 Public IP로 동작하거나 동일한 NAT로부터 IP를 받아야 한다. (즉 동일한 Subnet에서 실행해야 한다)
 - MainServer에서는 각 프로토콜(TCP or UDP) 마다 2개의 Port를 열어야 하는데, Client 입장에서 봤을 때 두 Port의 IP는 반드시 동일해야 한다. (*Address_Dependent*와 *Address_and_Port_Dependent*를 구분하기 위함)
 - Client 입장에서 봤을 때 MainServer와 SubServer는 IP가 달라야 한다. (*Endpoint_Independent*와 다른 것들을 구분하기 위함) Server들을 NAT 뒤에서 실행 할 때 주의 요망.
 - Clinet 측에서 실행중인 방화벽 등의 보안프로그램들이 테스트에 영향을 줄 수 있다. (주로 FilteringBehavior에 관련)


## 테스트 방법 예시1 - Mobile 통신망의 CGN을 테스트 하기
 1. 2개의 Cloud Server를 띄우고 하나엔 [SampleMainServer]를, 다른 하나엔 [SampleSubServer]를 실행 (위의 주의사항 숙지)
 2. 테스트 할 통신사의 스마트폰에 [SampleUnityClient]를 설치 ([Unity5] 사용)
 3. Wi-Fi를 끄고 4G(또는 3G)만 켠 후 앱 실행하여 테스트를 수행


## 테스트 방법 예시2 - 특정 공유기를 테스트 하기
 1. 준비물 : 공유기A(인터넷역할), 공유기B(테스트대상), 머신A(MainServer역할), 머신B(SubServer역할), 머신C(Client역할)
 2. 공유기A 밑에 공유기B, 머신A, 머신B를 연결
 3. 공유기B의 내부IP대역을 공유기A와 다르게 설정 (내부주소와 외부주소 구분을 쉽게 하기 위함)
 4. 공유기B 밑에 머신C를 연결
 5. 머신A에 [SampleMainServer]를, 머신B에 [SampleSubServer]를 실행 (위의 주의사항 숙지)
 6. 머신C에 [SampleClient] 또는 [SampleUnityClient]를 실행하여 테스트 수행


## 참고 문서
 - 용어는 Netmanias Blog([1][글1],[2][글2])와 [RFC4787]에 나오는 용어를 그대로 사용. (**EIM**=*Endpoint-Independent Mapping*, **APDF**=*Address and Port-Dependent Filtering*)
 - https://tools.ietf.org/html/rfc4787
 - https://docs.google.com/document/d/16O0IO39XICpGrHrGK9Vl_Fy5cgkjibZMzQjhY77j-HE/pub
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5833
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5839
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5841
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5854
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5856

[RFC4787]: https://tools.ietf.org/html/rfc4787
[글1]: http://www.netmanias.com/ko/?m=view&id=blog&no=5833
[글2]: http://www.netmanias.com/ko/?m=view&id=blog&no=5839
[SampleClient]: https://github.com/wlsgur0726/NAT_Test/tree/master/Sample/SampleClient
[SampleMainServer]: https://github.com/wlsgur0726/NAT_Test/tree/master/Sample/SampleMainServer
[SampleSubServer]: https://github.com/wlsgur0726/NAT_Test/tree/master/Sample/SampleSubServer
[SampleUnityClient]: https://github.com/wlsgur0726/NAT_Test/tree/master/Sample/SampleUnityClient
[Unity5]: https://unity3d.com/
