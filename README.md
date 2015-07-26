# NAT_Test
Client가 속한 NAT의 behavior를 테스트한다.


# 테스트 시 주의사항
 - Client, MainServer, SubServer는 각기 다른 호스트에서 실행해야 한다.
 - MainServer와 SubServer는 테스트 대상 NAT의 바깥에서 실행해야 한다.
 - MainServer와 SubServer 그리고 테스트 대상 NAT는 완전한 Public IP로 동작하거나 동일한 NAT로부터 IP를 받아야 한다. (즉 동일한 Subnet에서 실행해야 한다)
 - MainServer에서는 2개의 UDP Port를 열어야 하는데, Client 입장에서 봤을 때 둘의 IP는 반드시 동일해야 한다. (Address_Dependent와 Address_and_Port_Dependent를 구분하기 위함)
 - Client 입장에서 봤을 때 MainServer와 SubServer는 IP가 달라야 한다. (Endpoint_Independent와 다른 것들을 구분하기 위함)
 - Client측 방화벽에서 Inbound를 열어줘야 한다. (FilteringBehavior 테스트 시 Outbound가 없었던 Port로부터도 메시지를 수신 가능하게 하기 위함)


# 참고 문서
 - https://docs.google.com/document/d/16O0IO39XICpGrHrGK9Vl_Fy5cgkjibZMzQjhY77j-HE/pub
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5833
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5839
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5841
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5854
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5856
