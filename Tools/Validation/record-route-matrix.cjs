const fs=require('fs'),path=require('path');
const folder='Assets/StormSim/Sibucao/Data/PhysicalBranches';
const entries=fs.readdirSync(folder).filter(n=>n.endsWith('.asset'));
const byGuid=new Map(entries.map(name=>[fs.readFileSync(path.join(folder,name+'.meta'),'utf8').match(/^guid: (\w+)/m)[1],name]));
const lines=['encounter,kind,choice,role,difficulty,home,continuous_status,notes'];
let centers=0;
for(let junction=1;junction<=9;junction++){
 const node=entries.find(n=>n.startsWith('B'+junction+'_')&&!/^B\d+_C\d+\.asset$/.test(n));
 if(!node)throw new Error('Missing node '+junction);
 const source=fs.readFileSync(path.join(folder,node),'utf8');
 const choices=[...source.split('  choices:')[1].split('  prompt:')[0].matchAll(/guid: (\w+)/g)].map(m=>byGuid.get(m[1]));
 if(choices.some(c=>!c))throw new Error('Broken choice reference '+node);
 centers+=choices.length;
 const routes=choices.map((name,index)=>{
  const text=fs.readFileSync(path.join(folder,name),'utf8');
  const min=+text.match(/^  minimumTier: (\d+)/m)[1]+1,max=+text.match(/^  maximumTier: (\d+)/m)[1]+1;
  return {name:'center',kind:'core',choice:index,note:'Asset '+name+'; local water tier '+min+'-'+max+'; verify availability in continuous run'};
 }).concat(['left','right'].map(name=>({name,kind:'side',choice:'n/a',note:'Complete work and merge; flooded availability must be recorded'})));
 for(const route of routes)for(const role of ['Student','Parent','Volunteer'])for(let difficulty=1;difficulty<=5;difficulty++)
  lines.push(['B'+junction+'_'+route.name,route.kind,route.choice,role,difficulty,0,'NOT RUN',route.note].join(','));
}
fs.writeFileSync('Captures/Batches4-7_20260930/continuous-coverage.csv',lines.join('\n')+'\n');
console.log(centers+' referenced center choices plus 18 side passages; '+(lines.length-1)+' role/difficulty rows. All NOT RUN.');
