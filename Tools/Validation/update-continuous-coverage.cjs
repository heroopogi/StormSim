// Apply only recorded production-input encounter completions to the existing matrix.
// A completed encounter is not a completed journey or participant acceptance.
const fs=require('fs'),path=require('path');
const base='Captures/Batches4-7_20260930', matrix=path.join(base,'continuous-coverage.csv');
const rows=fs.readFileSync(matrix,'utf8').trim().split(/\r?\n/).map(line=>line.split(','));
const attempts=[];
function visit(directory){
  if(!fs.existsSync(directory))return;
  for(const entry of fs.readdirSync(directory,{withFileTypes:true})){
    const file=path.join(directory,entry.name);
    if(entry.isDirectory()&&entry.name!=='saves')visit(file);
    else if(entry.isFile()&&entry.name==='journey.txt'){
      const text=fs.readFileSync(file,'utf8');
      const setup=text.match(/BEGIN[^\n]*role=(\w+) difficulty=(\d+) side=(\d+) home=(\d+)/);
      if(!setup)continue;
      const [,role,difficulty,side,home]=setup;
      const preview=/LAYOUT PREVIEW/.test(text);
      const passed=[...text.matchAll(/PASS encounter B(\d+) result=(\d+)/g)];
      for(const [,junction,resultText] of passed){
        const result=Number(resultText),encounter='B'+junction+'_'+(result===100?'left':result===101?'right':'center');
        const match=rows.find(row=>row[0]===encounter&&row[3]===role&&row[4]===difficulty&&row[5]===home&&(result>=100||row[2]===String(result)));
        if(!match)throw Error('No matching matrix row for '+file+' '+junction+' '+result);
        if(!preview||match[6]!=='PASS'){match[6]=preview?'PREVIEW':'PASS';match[7]=(preview?'Physical input with explicitly staged layout correction; ':'Recorded continuous encounter; ')+file.replaceAll('\\','/');}
      }
      attempts.push({file,role,difficulty:Number(difficulty),side:Number(side),home:Number(home),encounters:passed.length,preview,complete:/PASS JOURNEY/.test(text),failure:text.split(/\r?\n/).find(line=>/\bFAIL\b/.test(line))||null});
    }
  }
}
visit(path.join(base,'Journeys'));
fs.writeFileSync(matrix,rows.map(row=>row.join(',')).join('\n')+'\n');
const summary={generatedUtc:new Date().toISOString(),rows:rows.length-1,passedRows:rows.slice(1).filter(row=>row[6]==='PASS').length,previewRows:rows.slice(1).filter(row=>row[6]==='PREVIEW').length,attempts};
fs.writeFileSync(path.join(base,'continuous-attempts.json'),JSON.stringify(summary,null,2)+'\n');
console.log(JSON.stringify(summary,null,2));
