const fs=require('fs'),path=require('path');
const folder='Assets/StormSim/Sibucao/Art/Narration';
const manifest=JSON.parse(fs.readFileSync(path.join(folder,'manifest.json'),'utf8'));
const report=[];
if(JSON.stringify(manifest)!==JSON.stringify(JSON.parse(fs.readFileSync('Assets/StormSim/Sibucao/Resources/SibucaoNarrationText.json','utf8'))))throw new Error('Captions differ between installer and runtime');
for(const cue of manifest.cues){
 const wave=fs.readFileSync(path.join(folder,cue.key+'.wav'));
 if(wave.toString('ascii',0,4)!=='RIFF'||wave.toString('ascii',8,12)!=='WAVE')throw new Error('Invalid WAV '+cue.key);
 let format,data;
 for(let offset=12;offset+8<=wave.length;){const name=wave.toString('ascii',offset,offset+4),size=wave.readUInt32LE(offset+4);if(offset+8+size>wave.length)throw new Error('Truncated '+cue.key);if(name==='fmt ')format=wave.subarray(offset+8,offset+8+size);if(name==='data')data=wave.subarray(offset+8,offset+8+size);offset+=8+size+(size%2);}
 if(!format||!data||format.readUInt16LE(0)!==1||format.readUInt16LE(14)!==16)throw new Error('Expected PCM16 '+cue.key);
 let energy=0,peak=0;for(let i=0;i<data.length;i+=2){const v=data.readInt16LE(i)/32768;energy+=v*v;peak=Math.max(peak,Math.abs(v));}
 const rms=Math.sqrt(energy/(data.length/2)),duration=data.length/format.readUInt32LE(8);
 if(rms<.001||duration<1)throw new Error('Silent or short clip '+cue.key);
 if(/press E|hold E|press Space|W A S D|two points per second|uses five/i.test(cue.text))throw new Error('Stale voiced instructions '+cue.key);
 report.push({key:cue.key,seconds:+duration.toFixed(2),rms:+rms.toFixed(4),peak:+peak.toFixed(4),status:'PASS PCM/content consistency; audible review pending'});
}
fs.writeFileSync('Captures/Batches4-7_20260930/narration-checks.json',JSON.stringify(report,null,2)+'\n');
console.log(report.length+' WAV/caption checks passed. Audible and in-game mix review not performed.');
