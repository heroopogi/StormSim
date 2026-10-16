const fs=require('fs'),path=require('path'),cp=require('child_process');
const root=path.resolve(__dirname,'../..');process.chdir(root);
const plan=JSON.parse(fs.readFileSync(process.argv[2]||'Temp/rc2-source-plan.json','utf8'));
for(const kind of ['Runtime','Editor']){
  let xml=fs.readFileSync(kind==='Runtime'?'StormSim.Validation.csproj':'StormSim.Validation.Editor.csproj','utf8');
  const drafts=plan.drafts.filter(([,target])=>kind==='Editor'?target.includes('/Editor/'):target.includes('/Runtime/'));
  const replacements=new Map(drafts.map(([draft,target])=>[target.toLowerCase(),'Tools/Validation/'+draft]));
  const found=new Set();
  xml=xml.replace(/<Compile Include="([^"]+)"\s*\/>/g,(all,file)=>{const key=file.replaceAll('\\','/').toLowerCase();if(!replacements.has(key))return all;found.add(key);return '<Compile Include="'+replacements.get(key)+'" />';});
  const added=[...replacements].filter(([key])=>!found.has(key)).map(([,draft])=>'<Compile Include="'+draft+'" />').join('\n');
  xml=xml.replace('</Project>','<ItemGroup>'+added+'</ItemGroup></Project>');
  if(kind==='Editor')xml=xml.replace(/(<Reference Include="Assembly-CSharp">\s*<HintPath>)[^<]+/, '$1Temp/RC2Draft/Runtime/Assembly-CSharp.dll');
  const project='StormSim.RC2Draft.'+kind+'.csproj';fs.writeFileSync(project,xml);
  const run=cp.spawnSync('dotnet',['build',project,'-v:q','-p:OutputPath=Temp/RC2Draft/'+kind+'/','-p:IntermediateOutputPath=Temp/RC2Draft/obj/'+kind+'/'],{encoding:'utf8',maxBuffer:15*1024*1024});
  const output=run.stdout+'\n'+run.stderr;fs.writeFileSync('Captures/Batches4-7_20260930/compile-'+(process.argv[2]?'followup':'rc2')+'-draft-'+kind.toLowerCase()+'.txt',output);
  console.log(kind+': exit '+run.status+'\n'+output.split('\n').filter(x=>/error |Error\(s\)|Warning\(s\)|Time Elapsed/.test(x)).join('\n'));
  if(run.status!==0)process.exit(run.status||1);
}
