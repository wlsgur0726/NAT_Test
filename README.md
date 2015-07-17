# NAT_Test
Client가 속한 NAT의 behavior를 테스트한다.
중첩 NAT인 경우 최상단의 NAT를 테스트한다.


# 테스트 시 주의사항
 - MainServer와 SubServer는 테스트 할 NAT의 바깥에서 실행해야 한다.
 - MainServer에서는 2개의 UDP Port를 열어야 하는데 둘의 Public IP는 반드시 동일해야 한다.
 - MainServer와 SubServer는 반드시 Public IP가 달라야 한다. (따라서 두 서버는 같은 NAT 밑에서 실행해선 안된다.)


# 참고 문서
 - https://docs.google.com/document/d/16O0IO39XICpGrHrGK9Vl_Fy5cgkjibZMzQjhY77j-HE/pub
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5833
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5839
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5841
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5854
 - http://www.netmanias.com/ko/?m=view&id=blog&no=5856
