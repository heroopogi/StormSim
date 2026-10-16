const fs=require('fs'),path=require('path'),crypto=require('crypto');
const plan=JSON.parse(fs.readFileSync('Temp/deck-pet-correction-plan.json','utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
for(const p of plan){if(hash(fs.readFileSync(p.path))!==p.before||hash(fs.readFileSync(p.draft))!==p.after)throw Error('Source changed after staging: '+p.path);}
for(const p of plan){const backup=path.join('Backups/Batches4-7_20260930/BeforeDeckPetCorrections',p.path);fs.mkdirSync(path.dirname(backup),{recursive:true});fs.writeFileSync(backup,fs.readFileSync(p.path),{flag:'wx'});}
for(const p of plan)fs.writeFileSync(p.path,fs.readFileSync(p.draft));
fs.writeFileSync('Captures/Batches4-7_20260930/deck-pet-source-changes.json',JSON.stringify(plan,null,2));console.log('Applied '+plan.length+' guarded source corrections');
