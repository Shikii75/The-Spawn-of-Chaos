(() => {
  const canvas = document.getElementById('bg');
  const ctx = canvas.getContext('2d');
  let w = canvas.width = innerWidth;
  let h = canvas.height = innerHeight;
  window.addEventListener('resize', () => { w = canvas.width = innerWidth; h = canvas.height = innerHeight; });

  // Particles for ambient chaos
  const particles = [];
  const colors = ['#0ff6ff','#b88cff','#ffd97b','#ff8fa3','#5effc7'];
  for (let i=0;i<120;i++) particles.push({x:Math.random()*w,y:Math.random()*h,vx:(Math.random()-0.5)*0.2,vy:(Math.random()-0.5)*0.2,size:Math.random()*2+0.4,color:colors[i%colors.length],alpha:Math.random()*0.6+0.15});

  function update(){
    ctx.clearRect(0,0,w,h);
    // soft radial gradient
    const g = ctx.createLinearGradient(0,0,w,h);
    g.addColorStop(0,'#06060a'); g.addColorStop(1,'#040308');
    ctx.fillStyle = g; ctx.fillRect(0,0,w,h);

    // draw particles
    for (let p of particles){
      p.x += p.vx; p.y += p.vy;
      if (p.x< -10) p.x = w+10; if (p.x> w+10) p.x = -10;
      if (p.y< -10) p.y = h+10; if (p.y> h+10) p.y = -10;
      ctx.globalAlpha = p.alpha;
      ctx.fillStyle = p.color;
      ctx.beginPath(); ctx.arc(p.x,p.y,p.size,0,Math.PI*2); ctx.fill();
    }

    // subtle noise overlay
    ctx.globalAlpha = 0.035;
    for (let i=0;i<35;i++){
      ctx.fillStyle = colors[(i+7)%colors.length];
      ctx.fillRect((Math.random()*w)|0,(Math.random()*h)|0,1,1);
    }
    ctx.globalAlpha = 1;

    requestAnimationFrame(update);
  }
  update();

  // Title shimmer using CSS class toggles for performance
  const title = document.getElementById('title');
  setInterval(()=>{
    title.style.filter = 'drop-shadow(0 22px 40px rgba(0,0,0,0.85))';
    title.style.opacity = 0.98;
    setTimeout(()=>{title.style.filter='drop-shadow(0 8px 30px rgba(0,0,0,0.8))';},240);
  },3000);

  // Letter wave when hovering title
  title.addEventListener('mousemove', (e)=>{
    const rect = title.getBoundingClientRect();
    const x = (e.clientX - rect.left)/rect.width;
    title.style.transform = `translateY(${(x-0.5)*-10}px) rotate(${(x-0.5)*1}deg)`;
  });
  title.addEventListener('mouseleave', ()=>{ title.style.transform = ''; });

  // Buttons
  const startBtn = document.getElementById('startBtn');
  const continueBtn = document.getElementById('continueBtn');
  const optionsBtn = document.getElementById('optionsBtn');
  const quitBtn = document.getElementById('quitBtn');

  function pulse(btn){ btn.animate([{transform:'scale(1)'},{transform:'scale(1.04)'}],{duration:260,iterations:1,easing:'ease-out'}); }
  startBtn.addEventListener('click', ()=>{ pulse(startBtn); console.log('Start pressed — hook into game bootstrap'); startBtn.disabled=true; startBtn.textContent='Launching...'; setTimeout(()=>startBtn.textContent='Start Game',1200); });
  continueBtn.addEventListener('click', ()=>{ pulse(continueBtn); console.log('Continue pressed — load save'); });
  optionsBtn.addEventListener('click', ()=>{ pulse(optionsBtn); alert('Options — placeholder'); });
  quitBtn.addEventListener('click', ()=>{ pulse(quitBtn); console.log('Quit pressed — close window if applicable'); if (navigator.userAgent.indexOf('Electron')>-1) window.close(); });

  // small accessibility: keyboard control
  document.addEventListener('keydown',(e)=>{ if (e.key==='Enter') startBtn.click(); if (e.key==='o') optionsBtn.click(); });
})();