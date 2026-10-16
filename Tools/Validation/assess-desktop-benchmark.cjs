const fs=require('fs'),path=require('path');
const label=process.argv[2];if(!label||!/^[a-zA-Z0-9-]+$/.test(label))throw Error('Supply a standalone report label');
const folder=path.join('Captures/Batches4-7_20260930/Standalone',label);
const report=JSON.parse(fs.readFileSync(path.join(folder,'desktop-benchmark.json'),'utf8').replace(/^\uFEFF/,''));
const lines=[];const check=(ok,label)=>lines.push((ok?'PASS ':'FAIL ')+label);
check(report.build==='2026.09.30-b4-7-rc2','Expected RC2 build identifier');
check(report.resolution==='1920x1080','1920 x 1080 reference viewport');
for(const name of ['first-junction-dry','middle-route-rising','school-late']){
  const sample=report.samples.find(s=>s.location===name);
  check(!!sample,name+' exists');if(!sample)continue;
  check(sample.viewpointValid&&sample.mode==='Playing',name+' has a valid gameplay viewpoint');
  check(sample.frames>=120,name+' has enough measured frames');
  check(sample.medianMs<=33.3&&sample.p95Ms<=50,name+' meets median <= 33.3 ms and p95 <= 50 ms; measured '+sample.medianMs.toFixed(2)+' / '+sample.p95Ms.toFixed(2)+' ms');
}
const log=fs.readFileSync(path.join(folder,'Player.log'),'utf8');
const startup={
  xrUnavailable:(log.match(/XR_ERROR_FORM_FACTOR_UNAVAILABLE/g)||[]).length,
  missingScripts:(log.match(/The referenced script[^\r\n]*missing/g)||[]).length,
  serializationMismatch:(log.match(/different serialization layout/g)||[]).length,
  graphicsRingBuffer:(log.match(/ring buffer[^\r\n]*(?:full|exhaust)|(?:full|exhaust)[^\r\n]*ring buffer/gi)||[]).length,
  exceptions:(log.match(/^[A-Za-z.]*Exception:/gm)||[]).length
};
check(startup.xrUnavailable===0,'No desktop XR form-factor startup failure');
check(startup.missingScripts===0&&startup.serializationMismatch===0,'No missing-script or serialization-layout startup failure');
check(startup.exceptions===0,'No managed exceptions in player log');
fs.writeFileSync(path.join(folder,'assessment.txt'),lines.join('\n')+'\n');
fs.writeFileSync(path.join(folder,'startup-categories.json'),JSON.stringify(startup,null,2)+'\n');
console.log(lines.join('\n'));console.log(JSON.stringify({startup,note:'Development-player staged profiling; not continuous traversal or participant acceptance.'}));
if(lines.some(line=>line.startsWith('FAIL ')))process.exitCode=1;
