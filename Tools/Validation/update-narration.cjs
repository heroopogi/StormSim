const fs=require('fs'),path=require('path');
const folder='Assets/StormSim/Sibucao/Art/Narration';
const backup='Backups/Batches4-7_20260930/Narration';
fs.mkdirSync(backup,{recursive:true});
for(const name of fs.readdirSync(folder))if(!fs.existsSync(path.join(backup,name)))fs.copyFileSync(path.join(folder,name),path.join(backup,name));
const manifest=JSON.parse(fs.readFileSync(path.join(folder,'manifest.json'),'utf8'));
const changes={
 home:'Prepare your go-bag before leaving. Open storage and carry the bag, then carry and pack the supplies you choose. Use the on-screen prompts for your controls. Check the door before leaving. Your guide lists the remaining preparation tasks.',
 swim:'You are now swimming. Use your movement controls and the rise or dive controls shown in the guide. Watch your breath and look for a marked landing where you can return to dry footing.',
 tired:'Your stamina is low. Slow down and rest on dry footing. Running, swimming, carrying and difficult water take more effort. Watch the stamina display and recover before sprinting again.',
 station_sibucao_d33:'A fallen palm blocks the first street. Face the trunk and follow the climbing prompt. Shift your footing when prompted. You can also inspect the surrounding lane.',
 station_sibucao_d34:'A supply cart has overturned. Inspect the sealed water bottle and food pack. Carry and pack the supplies you choose, following the on-screen controls. Then continue past the cart. You can also inspect the side lane.'
};
for(const cue of manifest.cues){
 if(changes[cue.key])cue.text=changes[cue.key];
 if(cue.key.startsWith('side_'))cue.text=cue.text.replace('Face the marked person or object and press E. Follow each step, then take this passage to the next meeting point.','Inspect the marked person or object and follow the action prompt. Complete the work, then follow the passage to the next meeting point.').replace(/\. ([a-z])/g,(_,c)=>'. '+c.toUpperCase());
 if(/^station_sibucao_d4[4-7]$/.test(cue.key))cue.text='The porch awning has collapsed. Release both rope knots, then take the lifting grip and walk backward to raise the awning. Follow the prompt to secure it before walking through. You can also inspect the side gap.';
}
const json=JSON.stringify(manifest,null,2)+'\n';
fs.writeFileSync(path.join(folder,'manifest.json'),json);
fs.mkdirSync('Assets/StormSim/Sibucao/Resources',{recursive:true});
fs.writeFileSync('Assets/StormSim/Sibucao/Resources/SibucaoNarrationText.json',json);
console.log('Updated '+manifest.cues.length+' captions. Audio generation must complete before validation.');
