const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'../..');const plan=JSON.parse(fs.readFileSync(path.join(root,'Temp/rc2-source-plan.json'),'utf8'));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
for(const item of plan.before){const f=path.join(root,item.path);const current=fs.existsSync(f)?sha(fs.readFileSync(f)):null;if(current!==item.sha256)throw Error('Source changed; merge explicitly: '+item.path);}
for(const [draft] of plan.drafts)fs.accessSync(path.join(root,'Tools/Validation',draft));
for(const item of plan.before){if(item.sha256===null)continue;const backup=path.join(root,'Backups/Batches4-7_20260930/BeforeRC2',item.path);fs.mkdirSync(path.dirname(backup),{recursive:true});if(fs.existsSync(backup))throw Error('Backup already exists: '+item.path);fs.copyFileSync(path.join(root,item.path),backup);}
for(const [draft,target] of plan.drafts)fs.copyFileSync(path.join(root,'Tools/Validation',draft),path.join(root,target));
for(const shader of plan.shaders){const f=path.join(root,shader);const original=fs.readFileSync(f,'utf8');if(!original.includes('#pragma multi_compile _ _FORWARD_PLUS'))throw Error('Shader keyword changed: '+shader);fs.writeFileSync(f,original.replaceAll('#pragma multi_compile _ _FORWARD_PLUS','#pragma multi_compile _ _CLUSTER_LIGHT_LOOP'));}
const benchmark=path.join(root,'Assets/StormSim/Sibucao/Runtime/SibucaoDesktopBenchmark.cs');fs.writeFileSync(benchmark,fs.readFileSync(benchmark,'utf8').replaceAll('2026.09.30-b4-7','2026.09.30-b4-7-rc2'));
const files=plan.before.map(item=>({path:item.path,before:item.sha256,after:sha(fs.readFileSync(path.join(root,item.path)))}));fs.writeFileSync(path.join(root,'Captures/Batches4-7_20260930/rc2-source-changes.json'),JSON.stringify({appliedUtc:new Date().toISOString(),files},null,2));console.log('Applied '+files.length+' reviewed source/shader updates with backups.');
