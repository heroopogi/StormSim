const fs=require('fs'),path=require('path'),cp=require('child_process');
const folder='Captures/Batches4-7_20260930';fs.mkdirSync(folder,{recursive:true});
for(const [source,target,extra] of [['Assembly-CSharp.csproj','StormSim.Validation.csproj','Runtime'],['Assembly-CSharp-Editor.csproj','StormSim.Validation.Editor.csproj','Editor']]){
  let xml=fs.readFileSync(source,'utf8');
  const pipeline=fs.readdirSync('Library/PackageCache').find(n=>n.startsWith('com.unity.pipeline@'));
  if(pipeline)xml=xml.replace(/<HintPath>Assets\\Plugins\\Roslyn\\([^<]+)<\/HintPath>/g,(_,name)=>'<HintPath>Library/PackageCache/'+pipeline+'/Runtime/Plugins/CodeAnalysis/'+name+'</HintPath>');
  const added=fs.readdirSync('Assets/StormSim/Sibucao/'+extra).filter(f=>f.endsWith('.cs')&&!xml.includes('\\'+f+'"')).map(f=>'<Compile Include="Assets/StormSim/Sibucao/'+extra+'/'+f+'" />').join('\n');
  xml=xml.replace('</Project>','<ItemGroup>'+added+'</ItemGroup></Project>');
  xml=xml.replace(/<ProjectReference Include="([^"]+)"\s*\/>/g,(_,ref)=>{const name=path.basename(ref,'.csproj');return '<Reference Include="'+name+'"><HintPath>'+(name==='Assembly-CSharp'?'Temp/bin/Debug/':'Library/ScriptAssemblies/')+name+'.dll</HintPath></Reference>';});
  fs.writeFileSync(target,xml);
  const run=cp.spawnSync('dotnet',['build',target,'-v:q'],{encoding:'utf8',maxBuffer:10*1024*1024});
  const output=run.stdout+'\n'+run.stderr;fs.writeFileSync(folder+'/compile-'+extra.toLowerCase()+'.txt',output);
  console.log(extra+': exit '+run.status+'\n'+output.split('\n').filter(x=>/error |Error\(s\)|Warning\(s\)|Time Elapsed/.test(x)).join('\n'));
  if(run.status!==0)process.exit(run.status||1);
}
