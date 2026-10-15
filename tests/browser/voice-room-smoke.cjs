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
 await page.locator('#voice-read').click();
 await page.waitForFunction(()=>!document.getElementById('voice-record').disabled,{},{timeout:10000});
 // Probe the real browser service first. A synthetic audio fixture is not a human microphone test.
 await page.locator('#voice-record').click();
 await page.waitForFunction(()=>!document.getElementById('voice-stop').disabled);
 await page.waitForTimeout(4000);
 const nativeSpeech={text:await page.locator('#voice-transcript').inputValue(),status:await page.locator('#voice-stt-status').textContent()};
 const nativeUpload=page.waitForResponse(r=>r.url().includes('handler=Upload')&&r.request().method()==='POST');
 await page.locator('#voice-stop').click();
 assert.equal((await nativeUpload).ok(),true);
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
 const artifacts=process.env.AIVES_BROWSER_ARTIFACTS || path.join(os.tmpdir(),'aives-voice-browser');
 fs.mkdirSync(artifacts,{recursive:true});
 await page.screenshot({path:path.join(artifacts,'voice-room-desktop.png'),fullPage:true});
 await page.setViewportSize({width:390,height:844});
 await page.screenshot({path:path.join(artifacts,'voice-room-mobile.png'),fullPage:true});
 await page.setViewportSize({width:1400,height:1000});
 await page.locator('#voice-submit').click();
 await page.waitForFunction(()=>document.getElementById('voice-status').textContent.includes('Đã hoàn thành'),{},{timeout:15000});
 assert.match(await page.locator('#voice-history').innerText(),/hoàn tất/);
 const links=page.locator('#voice-history a'); assert.ok(await links.count()>=1);
 const audio=await context.request.get(await links.last().getAttribute('href')); assert.equal(audio.status(),200);
 await page.screenshot({path:path.join(artifacts,'browser-flow.png'),fullPage:true});
 assert.deepEqual(errors,[]);
 const result={schedule:true,ttsControl:true,recorded:true,uploaded:true,submitted:true,audioStatus:audio.status(),autoSavedDraft:true,draftReload:true,recognitionEventFixture:true,nativeSpeech};
 fs.writeFileSync(path.join(artifacts,'browser-flow-result.json'),JSON.stringify(result,null,2));
 console.log('VOICE_BROWSER_FLOW_OK '+JSON.stringify(result));
 }finally{await browser.close()}
})().catch(e=>{console.error(e.message);process.exitCode=1});
