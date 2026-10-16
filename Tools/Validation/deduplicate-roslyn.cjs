const fs=require('fs'),path=require('path');
// Unity.Pipeline already provides the five runtime/compiler dependencies. Keep the local
// overrides backed up on disk, but exclude them from import instead of shipping two versions.
const folder='Assets/Plugins/Roslyn',backup='Backups/Batches4-7_20260930/RoslynMetadata';
fs.mkdirSync(backup,{recursive:true});
for(const name of fs.readdirSync(folder).filter(n=>n.endsWith('.dll.meta'))){
 const file=path.join(folder,name),old=fs.readFileSync(file,'utf8');
 if(!fs.existsSync(path.join(backup,name)))fs.writeFileSync(path.join(backup,name),old);
 const guid=old.match(/^guid: (.+)$/m)[1].trim();
 fs.writeFileSync(file,`fileFormatVersion: 2\nguid: ${guid}\nPluginImporter:\n  externalObjects: {}\n  serializedVersion: 3\n  iconMap: {}\n  executionOrder: {}\n  defineConstraints: []\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n  validateReferences: 1\n  platformData:\n    Any:\n      enabled: 0\n      settings: {}\n    Editor:\n      enabled: 0\n      settings:\n        DefaultValueInitialized: true\n  userData: Redundant override disabled; dependencies supplied by com.unity.pipeline.\n  assetBundleName:\n  assetBundleVariant:\n`);
}
console.log('Excluded five duplicate local dependencies. Unity import and ExecuteCode smoke check remain required.');
