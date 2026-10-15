const {chromium}=require(process.env.AIVES_PLAYWRIGHT_MODULE || 'playwright-core');
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),os=require('node:os');
(async()=>{
 const args=['--use-fake-device-for-media-stream','--use-fake-ui-for-media-stream'];
 if(process.env.AIVES_TEST_AUDIO_FILE) args.push('--use-file-for-fake-audio-capture='+process.env.AIVES_TEST_AUDIO_FILE);
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args});
 try {
 const context=await browser.newContext({permissions:['microphone'],viewport:{width:1400,height:1000}});
 const page=await context.newPage(); const errors=[]; page.on('pageerror',e=>errors.push(e.message));
 let voiceSocket;
 await page.routeWebSocket('**/hubs/voice-room*',ws=>{voiceSocket=ws;ws.connectToServer()});
 const base=process.env.AIVES_TEST_URL;
 await page.goto(base+'/Account/Login');
 await page.locator('[name="Input.Email"]').fill(process.env.AIVES_TEST_LOGIN);
 await page.locator('[name="Input.Password"]').fill(process.env.AIVES_TEST_PASSWORD);
 await Promise.all([page.waitForURL(u=>!u.pathname.includes('/Login')),page.locator('button[type=submit]').click()]);
 await page.goto(base+'/StudentSchedule/Mine');
 const voice=page.getByRole('button',{name:'Thi giọng nói',exact:true});
 assert.equal(await voice.isEnabled(),true);
 await Promise.all([page.waitForURL('**/VoiceRoom/1'),voice.click()]);
 await page.locator('#voice-question').filter({hasText:'Second question'}).waitFor();
 assert.equal(await page.locator('.voice-steps').isVisible(),false);
 assert.equal(await page.locator('.voice-backups').isVisible(),false);
 assert.equal(await page.locator('#voice-submit').isVisible(),false);
 assert.equal(await page.locator('#voice-stt-retry').isVisible(),false);
 assert.equal(await page.locator('#voice-stop').isVisible(),false);
 assert.equal(await page.locator('#voice-record').innerText(),'Bắt đầu trả lời');
 await page.evaluate(()=>{
  window.realGetUserMedia=navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
  navigator.mediaDevices.getUserMedia=async()=>{throw new DOMException('Test denied','NotAllowedError')};
 });
 await page.locator('#voice-record').click();
 await page.locator('#voice-capture-status').filter({hasText:'Bạn chưa cho phép microphone'}).waitFor();
 assert.equal(await page.locator('#voice-record').isEnabled(),true);
 await page.evaluate(()=>{
  window.realSpeak=speechSynthesis.speak.bind(speechSynthesis);
  speechSynthesis.speak=utterance=>queueMicrotask(()=>utterance.onerror?.());
 });
 await page.locator('#voice-read').click();
 await page.locator('#voice-capture-status').filter({hasText:'Không đọc được tự động'}).waitFor();
 await page.evaluate(()=>{speechSynthesis.speak=window.realSpeak});
 await page.evaluate(()=>{navigator.mediaDevices.getUserMedia=constraints=>new Promise(resolve=>{window.allowTestMic=()=>resolve(window.realGetUserMedia(constraints))})});
 await page.locator('#voice-read').click();
 await page.waitForFunction(()=>!document.getElementById('voice-record').disabled,{},{timeout:10000});
 // Probe the real browser service first. A synthetic audio fixture is not a human microphone test.
 await page.locator('#voice-record').click();
 await page.waitForFunction(()=>typeof window.allowTestMic==='function');
 assert.equal(await page.locator('#voice-stop').innerText(),'Chờ quyền mic…');
 assert.equal(await page.locator('#voice-stop').isEnabled(),false);
 await page.evaluate(()=>{window.allowTestMic();navigator.mediaDevices.getUserMedia=window.realGetUserMedia});
 await page.waitForFunction(()=>!document.getElementById('voice-stop').disabled);
 assert.equal(await page.locator('#voice-record').isVisible(),false);
 assert.equal(await page.locator('#voice-stop').innerText(),'Dừng thu âm');
 await page.waitForTimeout(4000);
 const nativeSpeech={text:await page.locator('#voice-transcript').inputValue(),status:await page.locator('#voice-stt-status').textContent()};
 const nativeUpload=page.waitForResponse(r=>r.url().includes('handler=Upload')&&r.request().method()==='POST');
 await page.locator('#voice-stop').click();
 assert.equal((await nativeUpload).ok(),true);
 await page.waitForFunction(()=>!window.voiceCapture.busy);
 nativeSpeech.text=await page.locator('#voice-transcript').inputValue();
 nativeSpeech.status=await page.locator('#voice-stt-status').textContent();
 await page.evaluate(()=>{
  window.SpeechRecognition=undefined;window.webkitSpeechRecognition=undefined;
 });
 await page.locator('#voice-record').click();
 await page.waitForFunction(()=>document.getElementById('voice-room').dataset.speechState==='unsupported');
 assert.equal(await page.locator('#voice-stt-status').isVisible(),true);
 assert.equal(await page.locator('#voice-stt-retry').isVisible(),false);
 await page.waitForTimeout(1100);
 const unsupportedUpload=page.waitForResponse(r=>r.url().includes('handler=Upload')&&r.request().method()==='POST');
 await page.locator('#voice-stop').click();assert.equal((await unsupportedUpload).ok(),true);
 await page.waitForFunction(()=>!window.voiceCapture.busy);
 await page.locator('#voice-transcript').fill('');
 // Deterministic recognition events exercise final-result flush and real storage, without pretending to test the remote STT engine.
 await page.evaluate(()=>{
  window.SpeechRecognition=class {
   start(){this.onstart?.();setTimeout(()=>this.onresult?.({results:[[{transcript:'Đây là lời nói đang được nhận dạng'}]]}),40)}
   stop(){setTimeout(()=>{this.onresult?.({results:[Object.assign([{transcript:'Đây là lời nói đã chuyển thành văn bản hoàn tất.'}],{isFinal:true})]});this.onend?.()},60)}
   abort(){}
  };
 });
 await page.locator('#voice-record').click();
 await page.waitForFunction(()=>!document.getElementById('voice-stop').disabled);
 await page.waitForTimeout(1100);
 const response=page.waitForResponse(r=>r.url().includes('handler=Upload')&&r.request().method()==='POST');
 await page.locator('#voice-stop').click();
 const uploaded=await response; assert.equal(uploaded.ok(),true,await uploaded.text());
 assert.equal(await page.locator('#voice-transcript').inputValue(),'Đây là lời nói đã chuyển thành văn bản hoàn tất.');
 await page.locator('#voice-submit').waitFor({state:'visible'});
 await page.waitForFunction(async()=>{
  const root=document.getElementById('voice-room');
  const response=await fetch(root.dataset.sync,{method:'POST',headers:{'Content-Type':'application/json',RequestVerificationToken:root.querySelector('[name="__RequestVerificationToken"]').value},body:'null'});
  const state=await response.json();return state.question?.transcript?.includes('hoàn tất.')&&state.question.hasAudio;
 },{},{timeout:7000});
 const draftSaved=page.waitForResponse(r=>r.url().includes('handler=Sync')&&r.request().method()==='POST'&&r.request().postData()?.includes('hoàn tất.')&&!r.request().postData()?.includes('"submit":true'));
 await page.locator('#voice-save').click();await draftSaved;
 const local=await page.evaluate(()=>window.voiceRecovery.list());
 assert.ok(local.some(x=>x.kind==='text'&&x.value?.includes('hoàn tất.')));
 assert.ok(local.some(x=>x.kind==='audio'&&x.synced));
 await page.reload();
 await page.waitForFunction(()=>document.getElementById('voice-transcript').value.includes('hoàn tất.'));
 await page.waitForFunction(()=>!document.getElementById('voice-preview').hidden&&document.getElementById('voice-local-badge').textContent==='Đã giữ trên thiết bị');
 await page.locator('#voice-preview').click({position:{x:18,y:20}});
 await page.waitForFunction(()=>document.getElementById('voice-preview').currentTime>0.05,{},{timeout:10000});
 await page.evaluate(()=>document.getElementById('voice-preview').pause());
 // UI failures must remain actionable without changing APIs or shared exam data.
 await page.route('**/*handler=Upload*',route=>route.fulfill({status:503,contentType:'application/json',body:JSON.stringify({error:'Kiểm thử lỗi lưu âm thanh.'})}));
 await page.locator('#voice-record').click();
 await page.waitForFunction(()=>!document.getElementById('voice-stop').disabled);
 await page.waitForTimeout(1100);
 await page.locator('#voice-stop').click();
 await page.locator('#voice-capture-status').filter({hasText:'Kiểm thử lỗi lưu'}).waitFor();
 assert.equal(await page.locator('#voice-upload').isVisible(),true);
 assert.equal(await page.locator('#voice-submit').isVisible(),false);
 await page.unroute('**/*handler=Upload*');
 const retryUpload=page.waitForResponse(r=>r.url().includes('handler=Upload')&&r.request().method()==='POST');
 await page.locator('#voice-upload').click(); assert.equal((await retryUpload).ok(),true);
 await page.locator('#voice-submit').waitFor({state:'visible'});
 await context.setOffline(true);
 assert.ok(voiceSocket,'the real SignalR websocket must be established');
 await voiceSocket.close({code:1012,reason:'Test reconnect'});
 await page.waitForFunction(()=>document.getElementById('voice-connection').textContent.includes('Mất kết nối'),{},{timeout:15000});
 assert.equal(await page.locator('#voice-connection').isVisible(),true);
 assert.equal(await page.locator('#voice-retry').isVisible(),true);
 await context.setOffline(false);
 await page.waitForFunction(()=>/Đã kết nối/.test(document.getElementById('voice-connection').textContent),{},{timeout:15000});
 await page.waitForFunction(()=>document.getElementById('voice-draft-summary').textContent.includes('Đã lưu audio và văn bản'));
 const artifacts=process.env.AIVES_BROWSER_ARTIFACTS || path.join(os.tmpdir(),'aives-voice-browser');
 fs.mkdirSync(artifacts,{recursive:true});
 await page.evaluate(()=>window.scrollTo(0,0));
 await page.screenshot({path:path.join(artifacts,'voice-room-desktop.png'),fullPage:true});
 await page.setViewportSize({width:390,height:844});
 await page.evaluate(()=>window.scrollTo(0,0));
 assert.ok(await page.evaluate(()=>{const box=document.getElementById('voice-room').getBoundingClientRect();return box.left>=0&&box.right<=innerWidth+1}),'voice room must fit a phone viewport');
 assert.ok(await page.evaluate(()=>{
  const nodes=['.voice-question-panel','.voice-recorder','.voice-transcript-panel','.voice-form-actions'].map(s=>document.querySelector(s).getBoundingClientRect());
  return nodes.every((box,i)=>i===0||box.top>=nodes[i-1].bottom-1);
 }),'mobile must follow question, recording, transcript, submission order');
 assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'page must not overflow horizontally');
 await page.screenshot({path:path.join(artifacts,'voice-room-mobile.png'),fullPage:true});
 await page.setViewportSize({width:1400,height:1000});
 await page.locator('#voice-submit').click();
 await page.waitForFunction(()=>document.getElementById('voice-status').textContent.includes('Đã hoàn thành'),{},{timeout:15000});
 assert.match(await page.locator('#voice-history').innerText(),/hoàn tất/);
 const links=page.locator('#voice-history a'); assert.ok(await links.count()>=1);
 const audio=await context.request.get(await links.last().getAttribute('href')); assert.equal(audio.status(),200);
 await page.screenshot({path:path.join(artifacts,'browser-flow.png'),fullPage:true});
 assert.deepEqual(errors,[]);
 const result={schedule:true,ttsControl:true,ttsError:true,unsupportedStt:true,microphoneDenial:true,permissionPending:true,recorded:true,uploaded:true,submitted:true,audioStatus:audio.status(),audioPlayback:true,autoSavedDraft:true,draftReload:true,uploadErrorRecovery:true,reconnected:true,mobileOrder:true,recognitionEventFixture:true,nativeSpeech};
 fs.writeFileSync(path.join(artifacts,'browser-flow-result.json'),JSON.stringify(result,null,2));
 console.log('VOICE_BROWSER_FLOW_OK '+JSON.stringify(result));
 }finally{await browser.close()}
})().catch(e=>{console.error(e.stack);process.exitCode=1});
