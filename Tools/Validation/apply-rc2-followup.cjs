const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'../..');process.chdir(root);
const plan=JSON.parse(fs.readFileSync('Temp/rc2-followup-plan.json','utf8'));
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const backup='Backups/Batches4-7_20260930/BeforeRC2Followup';
if(fs.existsSync(backup))throw Error('Follow-up backup already exists; refuse to overwrite evidence');
for(const [draft,target] of plan.drafts){
  if(!fs.existsSync('Tools/Validation/'+draft))throw Error('Missing draft '+draft);
  if((fs.existsSync(target)?hash(target):null)!==plan.originalHashes[target])throw Error('Source changed since review: '+target);
}
const changes=[];
for(const [draft,target] of plan.drafts){
  const saved=path.join(backup,target);fs.mkdirSync(path.dirname(saved),{recursive:true});
  if(fs.existsSync(target))fs.copyFileSync(target,saved);
  fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync('Tools/Validation/'+draft,target);
  changes.push({path:target,before:plan.originalHashes[target],after:hash(target)});
}
fs.writeFileSync('Captures/Batches4-7_20260930/rc2-followup-source-changes.json',JSON.stringify({appliedUtc:new Date().toISOString(),changes},null,2)+'\n');
console.log('Applied '+changes.length+' reviewed follow-up sources with backups');
