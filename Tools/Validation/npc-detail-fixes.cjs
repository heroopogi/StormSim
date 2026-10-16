const fs=require('fs');function e(f,fn){fs.writeFileSync(f,fn(fs.readFileSync(f,'utf8')));}
e('Assets/StormSim/Sibucao/Runtime/SibucaoPlayer.cs',t=>t.replace('if(hands)hands.localPosition=new Vector3(0,-.25f+bob,.44f-gesture*.03f);','if(hands){hands.localPosition=new Vector3(0,-.25f+bob,.44f-gesture*.03f);hands.localRotation=Quaternion.identity;}'));
e('Assets/StormSim/Sibucao/Runtime/SibucaoResident.cs',t=>t.replace('Vector3 bed=route.Sample(routeDistance);if(Physics.Raycast','Vector3 bed=route.Sample(routeDistance);bed.x=transform.position.x;bed.z=transform.position.z;if(Physics.Raycast'));
