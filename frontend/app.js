'use strict';

const API = '/api/v1'; // Netlify proxy -> backend Amvera. Никаких секретов во frontend.
const $ = (id) => document.getElementById(id);
const form = $('transaction-form');
const state = { latest: null, lastJson: '' };

const presets = {
  normal: {clientId:'TEST-001', amount:2000, currency:'RUB', transactionType:'TRANSFER',senderCountry:'RU',receiverCountry:'RU',counterpartyName:'Test Company',counterpartyType:'COMPANY',operationsLast30Days:2,totalAmountLast30Days:3000},
  high: {clientId:'TEST-002', amount:10000, currency:'USD', transactionType:'TRANSFER',senderCountry:'RU',receiverCountry:'RU',counterpartyName:'Test International Company',counterpartyType:'COMPANY',operationsLast30Days:25,totalAmountLast30Days:2500000},
  anomaly: {clientId:'TEST-003', amount:200000, currency:'RUB', transactionType:'TRANSFER',senderCountry:'RU',receiverCountry:'RU',counterpartyName:'Example Partner',counterpartyType:'COMPANY',operationsLast30Days:10,totalAmountLast30Days:400000}
};

const money = (v) => Number(v).toLocaleString('ru-RU', { maximumFractionDigits: 2 });
const statusError = (s, data) => data?.error || data?.title || `Сервер вернул HTTP ${s}.`;

async function fetchJson(path, options={}) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 20000);
  try {
    const res = await fetch(API + path, {cache:'no-store',...options,signal:controller.signal});
    const raw = await res.text();
    let json = null;
    try { json = raw ? JSON.parse(raw) : null; } catch { /* error handled below */ }
    return {ok:res.ok,status:res.status,data:json};
  } finally { clearTimeout(timeout); }
}
function showError(message) {
  const box=$('request-error');box.textContent=message;box.hidden=false;
}
function hideError() { $('request-error').hidden=true; }
function setView(name) {
  for(const button of document.querySelectorAll('.nav-btn')) button.classList.toggle('active',button.dataset.view===name);
  for(const view of document.querySelectorAll('.view')) view.classList.toggle('active',view.id==='view-'+name);
  $('current-view-title').textContent={analysis:'Анализ операции',history:'История проверок',architecture:'Интеграция API'}[name];
  if(name==='history') loadHistory();
}
for (const button of document.querySelectorAll('.nav-btn')) button.addEventListener('click',()=>setView(button.dataset.view));

function applyPreset(name) {
  const data=presets[name];
  for (const [key,value] of Object.entries(data)) {
    const control=form.elements.namedItem(key);
    if (control) control.value=String(value);
  }
  for (const button of document.querySelectorAll('.preset')) button.classList.toggle('active',button.dataset.preset===name);
  refreshRate();
}
for (const button of document.querySelectorAll('.preset')) button.addEventListener('click',()=>applyPreset(button.dataset.preset));

function readInput() {
  const data={};
  for(const name of ['clientId','transactionType','currency','senderCountry','receiverCountry','counterpartyType','counterpartyName']) {
    data[name]=form.elements.namedItem(name).value.trim();
  }
  for(const name of ['amount','operationsLast30Days','totalAmountLast30Days']) {
    data[name]=Number(form.elements.namedItem(name).value);
  }
  data.currency=data.currency.toUpperCase();
  data.senderCountry=data.senderCountry.toUpperCase();
  data.receiverCountry=data.receiverCountry.toUpperCase();
  return data;
}
function addListItems(list, values) {
  list.replaceChildren();
  for(const item of values) {const li=document.createElement('li');li.textContent=item;list.append(li);}
}
function renderResult(data, type) {
  const integrated=type==='platform';
  const own=integrated?data.ownAssessment:data;
  const partner=integrated?data.partnerAnalytics:null;
  const score=integrated?data.combinedRiskScore:own.riskScore;
  const level=integrated?data.combinedRiskLevel:own.riskLevel;
  const recommendation=integrated?data.combinedRecommendation:own.recommendation;
  if (!own || typeof score!=='number') {showError('Ответ сервиса имеет неожиданный формат.');return;}
  $('result-placeholder').hidden=true;
  $('result-content').hidden=false;
  $('score-value').textContent=score;
  $('risk-level').textContent=level;
  $('risk-level').className='risk-pill '+level.toLowerCase();
  $('score-progress').style.width='100%';
  $('score-progress').parentNode.style.background=`conic-gradient(${level==='HIGH'?'#e16c63':level==='MEDIUM'?'#eba342':'#24b783'} ${Math.min(100,Math.max(0,score))}%, #e8edfb 0)`;
  $('primary-score').textContent=`${own.riskScore} баллов`;
  $('partner-score').textContent=`${partner?.additionalScore ?? 0} баллов`;
  $('integration-pill').textContent=integrated ? (data.integrationStatus==='COMBINED'?'Два API объединены':'Частичный результат') : 'Только основной API';
  $('partner-explanation').textContent=partner ? `${partner.status}: ${partner.explanation}` : 'Обновление backend ещё не опубликовано. Для интеграции двух API требуется /api/v1/platform/analyze.';
  $('recommendation').textContent=recommendation;
  addListItems($('flags-list'), [...(own.triggeredFlags??[]),...(partner?.additionalScore>0?[`ANALYTICS: ${partner.explanation}`]:[])].length ? [...(own.triggeredFlags??[]),...(partner?.additionalScore>0?[`ANALYTICS: ${partner.explanation}`]:[])] : ['Учебные правила риска не сработали.']);
  state.latest=data;
  state.lastJson=JSON.stringify(data,null,2);
  $('raw-json').textContent=state.lastJson;
}

form.addEventListener('submit',async(evt)=>{
  evt.preventDefault();hideError();
  if(!form.reportValidity()) return;
  const submit=$('submit-transaction');submit.disabled=true;submit.firstElementChild.textContent='Выполняется анализ...';
  try {
    const payload=readInput();
    const options={method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)};
    const first=await fetchJson('/platform/analyze', options);
    if(first.ok) renderResult(first.data,'platform');
    else if(first.status===404) {
      const fallback=await fetchJson('/risk-score',options);
      if(fallback.ok) renderResult(fallback.data,'basic');
      else showError(statusError(fallback.status,fallback.data));
    } else {
      const errors=first.data?.errors?Object.values(first.data.errors).flat().join('; '):'';
      showError(errors||statusError(first.status,first.data));
    }
  } catch (err) {showError(err.name==='AbortError'?'Время ожидания API превышено.':'Нет ответа от API. Проверьте Amvera и настройки Netlify proxy.');}
  finally {submit.disabled=false;submit.firstElementChild.textContent='Выполнить анализ';}
});

async function refreshRate() {
  const code=$('currency-select').value; $('rate-display').textContent='Загружаем курс...';
  try {
    const response=await fetchJson('/rates/'+encodeURIComponent(code));
    $('rate-display').textContent=response.ok ? `1 ${code} = ${money(response.data.rubPerUnit)} ₽ · курс от ${response.data.rateDate}` : `Курс временно недоступен (${response.status})`;
  } catch { $('rate-display').textContent='Не удалось получить курс валюты.'; }
}
$('currency-select').addEventListener('change',refreshRate);
$('refresh-rate').addEventListener('click',refreshRate);

async function checkConnection(){
  const indicator=$('api-status');indicator.className='connection unknown';indicator.lastChild.textContent=' Проверка API...';
  try{
    const main=await fetchJson('/transactions');
    if(!main.ok) throw new Error();
    indicator.className='connection ok';indicator.lastChild.textContent=' Основной API доступен';
    const partner=await fetchJson('/platform/status');
    $('partner-status-short').textContent=partner.ok
      ? ({AVAILABLE:'Доступен',NOT_CONFIGURED:'Не подключён',UNAVAILABLE:'Недоступен'}[partner.data?.partnerApi]||'Неизвестно')
      : 'Ожидает обновления';
  }catch{
    indicator.className='connection error';indicator.lastChild.textContent=' API недоступен';
    $('partner-status-short').textContent='Не проверен';
  }
}
$('check-connection').addEventListener('click',checkConnection);

async function loadHistory(){
  const table=$('history-body');table.replaceChildren();
  const loading=document.createElement('tr');const td=document.createElement('td');td.colSpan=5;td.className='empty-cell';td.textContent='Загрузка транзакций...';loading.append(td);table.append(loading);
  try{
    const res=await fetchJson('/transactions');
    if(!res.ok || !Array.isArray(res.data)) throw new Error();
    table.replaceChildren();
    if(res.data.length===0){const tr=document.createElement('tr');const cell=document.createElement('td');cell.colSpan=5;cell.className='empty-cell';cell.textContent='Транзакций пока нет.';tr.append(cell);table.append(tr);}
    for(const item of res.data.slice().reverse()){
      const tr=document.createElement('tr');
      const fields=[String(item.transactionId??'').slice(0,8)+'…',item.clientId,`${money(item.amountRub)} ₽`,String(item.riskScore),item.riskLevel];
      fields.forEach((text,index)=>{const cell=document.createElement('td');if(index===4){const badge=document.createElement('span');badge.className='tag '+String(item.riskLevel).toLowerCase();badge.textContent=text;cell.append(badge);} else cell.textContent=text;tr.append(cell);});
      table.append(tr);
    }
    $('history-note').textContent=`Получено записей: ${res.data.length}. Хранятся только до перезапуска backend.`;
  } catch {table.replaceChildren();const tr=document.createElement('tr');const td=document.createElement('td');td.colSpan=5;td.className='empty-cell';td.textContent='Не удалось получить список транзакций.';tr.append(td);table.append(tr);}
}
$('refresh-history').addEventListener('click',loadHistory);
$('copy-json').addEventListener('click',async()=>{
  if (!state.lastJson) return;
  try {await navigator.clipboard.writeText(state.lastJson);$('copy-json').textContent='Скопировано ✓';setTimeout(()=>$('copy-json').textContent='Копировать JSON',1800);}catch{$('copy-json').textContent='Не удалось скопировать';}
});

checkConnection();
refreshRate();
