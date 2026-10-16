const fs=require('fs'),path=require('path'),crypto=require('crypto');
const manifest=[];for(const [current,before] of [['Assets/StormSim/Sibucao/Runtime','Backups/Batches4-7_20260930/Runtime'],['Assets/StormSim/Sibucao/Editor','Backups/Batches4-7_20260930/Editor'],['Assets/StormSim/Preparedness/Runtime','Backups/Batches4-7_20260930/PreparationRuntime']]){
 for(const file of fs.readdirSync(current).filter(f=>f.endsWith('.cs'))){const full=path.join(current,file),old=path.join(before,file),bytes=fs.readFileSync(full);if(!fs.existsSync(old)||!bytes.equals(fs.readFileSync(old)))manifest.push({path:full,status:fs.existsSync(old)?'modified':'new',sha256:crypto.createHash('sha256').update(bytes).digest('hex')});if(!fs.existsSync(full+'.meta'))fs.writeFileSync(full+'.meta','fileFormatVersion: 2\nguid: '+crypto.randomUUID().replaceAll('-','')+'\n');}
}
fs.writeFileSync('Captures/Batches4-7_20260930/source-manifest.json',JSON.stringify(manifest,null,2));
const p='Documentation/SIBUCAO_RAIN_IMPROVEMENT_CHECKLIST.md';let t=fs.readFileSync(p,'utf8').replace(/\r?\n\r?\n(\| 2026-09-30 \| Batches 4–7 in progress)/,'\n$1');fs.writeFileSync(p,t);
let tasks=[...t.matchAll(/^- \[([x ])\] \*\*(B\d+\.\d+)\*\*/gm)];let missing=[...t.matchAll(/\[[^\]]+\]\(([^)]+)\)/g)].map(m=>m[1]).filter(f=>!fs.existsSync(path.resolve('Documentation',f)));
console.log(JSON.stringify({changedSourceFiles:manifest.length,tasks:tasks.length,checked:tasks.filter(m=>m[1]==='x').length,missingLinks:missing}));
