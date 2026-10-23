const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const {readFileSync}=require('node:fs');
const {join}=require('node:path');
function setup(supported=true) {
 const instances=[],timers=new Map(),states=[],texts=[],errors=[];
 class Recognition {
  constructor(){instances.push(this)}
  start(){this.onstart?.()}
  stop(){this.stopCalled=true}
  abort(){}
  result(text,final=false){this.onresult({results:[Object.assign([{transcript:text}],{isFinal:final})]})}
 }
 const window=supported?{SpeechRecognition:Recognition}:{};
 vm.runInNewContext(readFileSync(join(__dirname,'../../src/AssignmentPRN.Presentation/UI Assets/js/voice-speech.js'),'utf8'),{
  window,Promise,setTimeout(fn){const id=Symbol();timers.set(id,fn);return id},clearTimeout(id){timers.delete(id)}
 });
 const adapter=window.createVoiceSpeechAdapter();
 adapter.start({onText:t=>texts.push(t),onError:e=>errors.push(e),onState:(s,m)=>states.push({s,m})});
 return {adapter,instances,timers,states,texts,errors};
}
test('stop waits for the final recognized text before resolving',async()=>{
 const h=setup(),engine=h.instances[0];
 engine.result('xin chào');
 let resolved=false;const stopped=h.adapter.stop().then(()=>{resolved=true});
 await Promise.resolve();assert.equal(resolved,false);
 engine.result('xin chào tôi xin trả lời',true);engine.onend();await stopped;
 assert.equal(h.texts.at(-1),'xin chào tôi xin trả lời');
 assert.equal(h.states.at(-1).s,'ready');assert.equal(resolved,true);
});
test('automatic recognition restarts retain the previous session text',()=>{
 const h=setup();h.instances[0].result('câu trả lời đầu',true);h.instances[0].onend();
 const [id,callback]=[...h.timers][0];h.timers.delete(id);callback();
 h.instances[1].result('và phần tiếp theo',true);
 assert.equal(h.texts.at(-1),'câu trả lời đầu và phần tiếp theo');
});
test('network failure remains visible after end instead of reporting successful recognition',async()=>{
 const h=setup();h.instances[0].onerror({error:'network'});h.instances[0].onend();await h.adapter.stop();
 assert.equal(h.states.at(-1).s,'error');assert.match(h.errors[0],/Không kết nối/);assert.equal(h.texts.length,0);
});
test('unsupported browser reports a usable text fallback',async()=>{
 const h=setup(false);await h.adapter.stop();assert.equal(h.adapter.supported,false);
 assert.equal(h.states.at(-1).s,'unsupported');assert.match(h.errors[0],/nhập văn bản/);
});
test('stop timeout preserves partial text and does not hang submission',async()=>{
 const h=setup();h.instances[0].result('nội dung tạm');const stopped=h.adapter.stop();
 const timeout=[...h.timers.values()][0];timeout();await stopped;
 assert.equal(h.texts.at(-1),'nội dung tạm');assert.equal(h.states.at(-1).s,'ready');
});
