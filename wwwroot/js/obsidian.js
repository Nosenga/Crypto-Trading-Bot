(()=>{const c=document.getElementById('bg'),x=c.getContext('2d'),c2=document.getElementById('bg2'),x2=c2.getContext('2d'),chip=document.getElementById('src'),fl=document.getElementById('flash'),c3=document.getElementById('mk'),x3=c3.getContext('2d');
const reduce=matchMedia('(prefers-reduced-motion: reduce)').matches,rnd=Math.random,CW=7,STEP=11,AX=64;
let W,H,D,marks=[],cs=[],seq=0,off=0,lo=0,hi=0,last=0,jt=0,simOn=false,bs=[],o2=0,p2=100;
const N=()=>Math.ceil(innerWidth/STEP)+6,N2=()=>Math.ceil(innerWidth/40)+3;
const mk=p=>{const o=p,cl=o+(rnd()-.48)*p*.0008;return{o,c:cl,h:Math.max(o,cl)+rnd()*p*.0004,l:Math.min(o,cl)-rnd()*p*.0004,v:rnd()}};
const push=k=>{window.__lastTick=Date.now();cs.push(k);seq++;if(cs.length>N())cs.shift();off-=STEP};
function simFill(){let p=cs.length?cs[cs.length-1].c:64000;while(cs.length<N()){const k=mk(p);cs.push(k);seq++;p=k.c}}
function fill2(){while(bs.length<N2()){const o=p2,cl=o+(rnd()-.5)*10;bs.push({o,c:cl,h:Math.max(o,cl)+rnd()*5,l:Math.min(o,cl)-rnd()*5});p2=Math.min(130,Math.max(70,cl))}}
const nice=r=>{const m=Math.pow(10,Math.floor(Math.log10(r))),f=r/m;return(f<1.5?1:f<3.5?2:f<7.5?5:10)*m};
function size(){D=Math.min(devicePixelRatio||1,2);W=c.width=c2.width=c3.width=innerWidth*D;H=c.height=c2.height=c3.height=innerHeight*D;fill2();if(simOn)simFill();draw();draw2()}
function draw(){if(c.style.display==='none'||!cs.length)return;x.clearRect(0,0,W,H);
const R=(innerWidth-AX)*D,n=cs.length,vis=cs.slice(-Math.ceil(R/D/STEP)-2);let a=1e9,b=-1e9,vm=1e-9;
for(const k of vis){a=Math.min(a,k.l);b=Math.max(b,k.h);vm=Math.max(vm,k.v)}
const mid=(a+b)/2,mr=mid*.0004;if(b-a<mr){a=mid-mr/2;b=mid+mr/2}
if(!lo){lo=a;hi=b}else{lo+=(a-lo)*.06;hi+=(b-hi)*.06}
const y=v=>H*.1+(1-(v-lo)/(hi-lo))*H*.72,X=j=>R-(j*STEP+off)*D;
x.setLineDash([2*D,6*D]);x.lineWidth=D;x.strokeStyle='rgba(255,255,255,.28)';x.fillStyle='rgba(255,255,255,.7)';x.font=(10*D)+'px "JetBrains Mono",monospace';x.textAlign='right';
const st=nice((hi-lo)/5);for(let p=Math.ceil(lo/st)*st;p<hi;p+=st){const yy=y(p);x.beginPath();x.moveTo(0,yy);x.lineTo(W,yy);x.stroke();x.fillText(p.toLocaleString(undefined,{maximumFractionDigits:2}),W-6*D,yy-3*D)}
for(let j=0;j<n;j++){const xx=X(j);if(xx<0)break;if((seq-1-j)%10===0){x.beginPath();x.moveTo(xx,0);x.lineTo(xx,H);x.stroke()}}
x.setLineDash([]);
for(let j=0;j<n;j++){const k=cs[n-1-j],xx=X(j);if(xx<-CW*D)break;const vh=k.v/vm*H*.12;x.fillStyle=k.c>=k.o?'rgba(255,255,255,.5)':'rgba(255,255,255,.2)';x.fillRect(xx-CW*D/2,H-vh-8*D,CW*D,vh)}
x.strokeStyle='#fff';
for(let j=0;j<n;j++){const k=cs[n-1-j],xx=X(j);if(xx<-CW*D)break;const up=k.c>=k.o,t=y(Math.max(k.o,k.c)),h=Math.max(D,Math.abs(y(k.o)-y(k.c)));
x.beginPath();x.moveTo(xx,y(k.h));x.lineTo(xx,y(k.l));x.stroke();x.fillStyle=up?'#fff':'#000';x.fillRect(xx-CW*D/2,t,CW*D,h);if(!up)x.strokeRect(xx-CW*D/2,t,CW*D,h)}
x3.clearRect(0,0,W,H);const nn=cs.length;marks=marks.filter(m=>X(seq-1-m.s)>-20*D);x3.textAlign='center';x3.font='600 '+9*D+'px "JetBrains Mono",monospace';
for(const m of marks){const j=seq-1-m.s,k=cs[nn-1-j];if(!k)continue;const xx=X(j),by=m.t==='buy',col=by?'#3ecf8e':m.t==='stop'?'#ff5c5c':'#fff',yy=by?y(k.l)+16*D:y(k.h)-16*D;
x3.fillStyle=x3.strokeStyle=col;x3.lineWidth=2*D;x3.beginPath();
if(m.t==='stop'){x3.moveTo(xx-5*D,yy-5*D);x3.lineTo(xx+5*D,yy+5*D);x3.moveTo(xx+5*D,yy-5*D);x3.lineTo(xx-5*D,yy+5*D);x3.stroke()}
else if(by){x3.moveTo(xx,yy-6*D);x3.lineTo(xx-6*D,yy+5*D);x3.lineTo(xx+6*D,yy+5*D);x3.fill()}
else{x3.moveTo(xx,yy+6*D);x3.lineTo(xx-6*D,yy-5*D);x3.lineTo(xx+6*D,yy-5*D);x3.fill()}
x3.fillText(m.t==='buy'?'BUY':m.t==='stop'?'STOP':'EXIT',xx,by?yy+18*D:yy-10*D)}}
function draw2(){x2.clearRect(0,0,W,H);const y=v=>H*(.85-(v-60)/90*.7);x2.strokeStyle='#fff';x2.lineWidth=2*D;
bs.forEach((k,i)=>{const xx=(i*40-o2+20)*D,t=y(Math.max(k.o,k.c)),h=Math.max(2*D,Math.abs(y(k.o)-y(k.c)));x2.beginPath();x2.moveTo(xx,y(k.h));x2.lineTo(xx,y(k.l));x2.stroke();x2.fillStyle=k.c>=k.o?'#fff':'#000';x2.fillRect(xx-13*D,t,26*D,h);x2.strokeRect(xx-13*D,t,26*D,h)})}
function loop(t){const dt=Math.min((t-(last||t))/1000,.05);last=t;off=Math.min(off+STEP*dt,STEP*1.6);o2+=6*dt;if(o2>=40){o2-=40;bs.shift();fill2()}
if(simOn&&cs.length&&(jt+=dt)>.14){jt=0;const k=cs[cs.length-1];k.c+=(rnd()-.5)*k.o*.0004;k.h=Math.max(k.h,k.c);k.l=Math.min(k.l,k.c)}
draw();draw2();requestAnimationFrame(loop)}
function setChip(t,on){chip.textContent=t;chip.className=on?'on':''}
function sim(){simOn=true;cs=[];seq=0;setChip(reduce ? 'decorative static' : 'decorative simulation', false);simFill();seedMarks();if(reduce){draw();return}setInterval(()=>push(mk(cs[cs.length-1].c)),1000)}
function go() {
  sim();
}
const seedMarks=()=>{marks=[]};
document.querySelector('.bgt').onclick=()=>{const h=c.style.display==='none';c.style.display=c2.style.display=c3.style.display=h?'':'none';if(h){draw();draw2()}};
addEventListener('resize',size);size();go();if(!reduce)requestAnimationFrame(loop)})();

(() => {
  const elementsBySymbol = new Map();
  document.querySelectorAll('[data-symbol]').forEach(element => {
    const symbol = element.dataset.symbol;
    if (!elementsBySymbol.has(symbol)) elementsBySymbol.set(symbol, []);
    elementsBySymbol.get(symbol).push(element);
  });
  if (!elementsBySymbol.size) return;

  if (!window.signalR) {
    console.error('[SignalR] Client library is unavailable.');
    return;
  }

  const connection = new window.signalR.HubConnectionBuilder()
    .withUrl('/tradingHub')
    .withAutomaticReconnect()
    .build();

  connection.on('MarketUpdate', prices => {
    if (!prices || typeof prices !== 'object') return;

    for (const [symbol, price] of Object.entries(prices)) {
      const elements = elementsBySymbol.get(symbol);
      const numericPrice = Number(price);
      if (!elements || !Number.isFinite(numericPrice)) continue;

      const formattedPrice = '$' + numericPrice.toLocaleString(undefined, {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      });
      elements.forEach(element => {
        element.textContent = formattedPrice;
      });
    }
  });

  connection.onreconnecting(error => {
    console.warn('[SignalR] Reconnecting to live market feed.', error);
  });
  connection.onreconnected(() => {
    console.info('[SignalR] Reconnected to live market feed.');
  });

  connection.start()
    .then(() => console.info('[SignalR] Connected to live market feed.'))
    .catch(error => console.error('[SignalR] Connection failed:', error));
})();

(() => {
  const palette = document.getElementById('pal');
  const input = document.getElementById('pin');
  const list = document.getElementById('pl');
  const button = document.getElementById('kbd');
  if (!palette || !input || !list || !button) return;

  const items = Array.from(list.querySelectorAll('li'));
  function filterItems() {
    const query = input.value.trim().toLowerCase();
    items.forEach(item => {
      item.hidden = !item.textContent.toLowerCase().includes(query);
    });
  }
  function openPalette() {
    palette.hidden = false;
    input.value = '';
    filterItems();
    input.focus();
  }
  button.addEventListener('click', openPalette);
  input.addEventListener('input', filterItems);
  palette.addEventListener('click', event => {
    if (event.target === palette) palette.hidden = true;
  });
  list.addEventListener('click', () => { palette.hidden = true; });
  document.addEventListener('keydown', event => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      palette.hidden ? openPalette() : (palette.hidden = true);
    } else if (event.key === 'Escape') {
      palette.hidden = true;
    }
  });
})();