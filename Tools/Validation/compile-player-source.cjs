// Compile the actual runtime sources with player symbols. This is not an asset import or player build.
const fs=require('fs'),cp=require('child_process');
for(const development of [true,false]){
 const label=development?'development':'release';
 let xml=fs.readFileSync('StormSim.Validation.csproj','utf8');
 xml=xml.replace(/<DefineConstants>([^<]*)<\/DefineConstants>/,(_,symbols)=>'<DefineConstants>'+symbols.split(';').filter(s=>!s.startsWith('UNITY_EDITOR')&&s!=='DEVELOPMENT_BUILD').concat(development?['DEVELOPMENT_BUILD']:[]).join(';')+'</DefineConstants>');
 const project='StormSim.Validation.Player.'+label+'.csproj';fs.writeFileSync(project,xml);
 const run=cp.spawnSync('dotnet',['build',project,'-v:q','-p:OutputPath=Temp/ValidationPlayer/'+label+'/','-p:IntermediateOutputPath=Temp/ValidationPlayer/obj/'+label+'/'],{encoding:'utf8',maxBuffer:15*1024*1024});
 const output=run.stdout+'\n'+run.stderr;fs.writeFileSync('Captures/Batches4-7_20260930/compile-player-source-'+label+'.txt',output);
 console.log(label+': exit '+run.status+'\n'+output.split('\n').filter(x=>/error |Error\(s\)|Warning\(s\)|Time Elapsed/.test(x)).join('\n'));
 if(run.status!==0)process.exit(run.status||1);
}
