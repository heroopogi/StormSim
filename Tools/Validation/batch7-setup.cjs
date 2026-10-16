const fs=require('fs');const root='Assets/StormSim/Sibucao/Runtime/';
function e(f,fn){fs.writeFileSync(root+f,fn(fs.readFileSync(root+f,'utf8')));}
e('SibucaoSessionSaves.cs',t=>t.replaceAll('#if UNITY_EDITOR','#if UNITY_EDITOR || DEVELOPMENT_BUILD'));
e('SibucaoRouteChoices.cs',t=>t.replace('if (c.centerVoice && c.centerVoice.clip) c.centerVoice.Play();','if (c.centerVoice && c.centerVoice.clip){c.centerVoice.volume=SibucaoAudioMix.Speech;c.centerVoice.Play();}'));
// Save exact originals before applying desktop-only startup and rendering settings.
for(const file of ['Assets/XR/XRGeneralSettingsPerBuildTarget.asset','Assets/Settings/PC_RPAsset.asset']){const dest='Backups/Batches4-7_20260930/'+file;fs.mkdirSync(require('path').dirname(dest),{recursive:true});fs.copyFileSync(file,dest);}
let p='Assets/XR/XRGeneralSettingsPerBuildTarget.asset',t=fs.readFileSync(p,'utf8');t=t.replace(/(m_Name: Standalone Settings[\s\S]*?m_InitManagerOnStart:) 1/,'$1 0');fs.writeFileSync(p,t);
p='Assets/Settings/PC_RPAsset.asset';t=fs.readFileSync(p,'utf8').replace('m_OpaqueDownsampling: 1','m_OpaqueDownsampling: 0');fs.writeFileSync(p,t);
