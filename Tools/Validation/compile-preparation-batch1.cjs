const fs=require('fs'),path=require('path'),cp=require('child_process');
const folder=process.argv[2]||'Reports/PreparationBatch1_20261013';fs.mkdirSync(folder,{recursive:true});
for(const [source,label] of [['Assembly-CSharp.csproj','Runtime'],['Assembly-CSharp-Editor.csproj','Editor']]){
 let xml=fs.readFileSync(source,'utf8');
 const pipeline=fs.readdirSync('Library/PackageCache').find(n=>n.startsWith('com.unity.pipeline@'));
 if(pipeline)xml=xml.replace(/<HintPath>Assets\\Plugins\\Roslyn\\([^<]+)<\/HintPath>/g,(_,name)=>'<HintPath>Library/PackageCache/'+pipeline+'/Runtime/Plugins/CodeAnalysis/'+name+'</HintPath>');
 const extra=[];
 for(const root of ['Assets/StormSim/Preparedness/'+label,'Assets/StormSim/Sibucao/'+label]){
  for(const file of fs.readdirSync(root).filter(f=>f.endsWith('.cs'))){
   const full=root+'/'+file;
   if(!xml.replaceAll('\\','/').includes('"'+full+'"'))extra.push('<Compile Include="'+full+'" />');
  }
 }
 xml=xml.replace('</Project>','<ItemGroup>'+extra.join('\n')+'</ItemGroup></Project>');
 xml=xml.replace(/<ProjectReference Include="([^"]+)"\s*\/>/g,(_,ref)=>{
  const name=path.basename(ref,'.csproj');
  return '<Reference Include="'+name+'"><HintPath>'+(name==='Assembly-CSharp'?'Temp/PrepBatch1/Runtime/':'Library/ScriptAssemblies/')+name+'.dll</HintPath></Reference>';
 });
 const project='StormSim.PreparationBatch1.'+label+'.csproj';fs.writeFileSync(project,xml);
 const run=cp.spawnSync('dotnet',['build',project,'-v:q','-p:OutputPath=Temp/PrepBatch1/'+label+'/','-p:IntermediateOutputPath=Temp/PrepBatch1/obj/'+label+'/'],{encoding:'utf8',maxBuffer:15*1024*1024});
 const output=run.stdout+'\n'+run.stderr;fs.writeFileSync(folder+'/compile-'+label.toLowerCase()+'.txt',output);
 console.log(label+': exit '+run.status+'\n'+output.split('\n').filter(x=>/error |Error\(s\)|Warning\(s\)|Time Elapsed/.test(x)).join('\n'));
 if(run.status!==0)process.exit(run.status||1);
}
