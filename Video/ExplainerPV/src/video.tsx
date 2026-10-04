import React from 'react';
import {
  AbsoluteFill,
  Audio,
  Easing,
  interpolate,
  OffthreadVideo,
  Sequence,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import type {DiagramKind, SceneManifest, VideoManifest} from './types';

const ink = '#06121b';
const cyan = '#7be7ec';
const gold = '#f1c975';

const clamp = {extrapolateLeft: 'clamp' as const, extrapolateRight: 'clamp' as const};
const ease = Easing.bezier(0.22, 0.7, 0.15, 1);

export const ExplainerVideo: React.FC<{manifest: VideoManifest}> = ({manifest}) => {
  const {fps} = useVideoConfig();
  let offset = 0;
  const clips = manifest.scenes.map((scene) => {
    const from = offset;
    const duration = Math.round(scene.durationSeconds * fps);
    offset += duration;
    return {scene, from, duration};
  });

  return (
    <AbsoluteFill style={{backgroundColor: ink, color: '#effcff', fontFamily: 'Yu Gothic, Meiryo, sans-serif'}}>
      {clips.map(({scene, from, duration}) => (
        <Sequence key={scene.id} from={from} durationInFrames={duration} name={scene.id}>
          <SceneView scene={scene} durationInFrames={duration} />
        </Sequence>
      ))}
      {manifest.audioMix ? <Audio src={staticFile(manifest.audioMix)} volume={1} /> : null}
    </AbsoluteFill>
  );
};

const SceneView: React.FC<{scene: SceneManifest; durationInFrames: number}> = ({scene, durationInFrames}) => {
  const frame = useCurrentFrame();
  const overlayOpacity = interpolate(frame, [0, 12, durationInFrames - 10, durationInFrames], [0, 1, 1, 0], clamp);
  const isTitle = scene.id === 'title' || scene.id === 'finale';
  const hasDiagram = Boolean(scene.diagram);
  const hasClip = Boolean(scene.clip);

  return (
    <AbsoluteFill style={{overflow: 'hidden'}}>
      {hasClip ? (
        <OffthreadVideo
          src={staticFile(scene.clip!)}
          startFrom={Math.round((scene.clipStartSeconds ?? 0) * 30)}
          muted
          style={{width: '100%', height: '100%', objectFit: 'cover', opacity: hasDiagram ? 0.54 : 1}}
        />
      ) : null}
      <AbsoluteFill style={{opacity: overlayOpacity}}>
        <OceanAtmosphere frame={frame} transparent={hasClip} />
        <AbsoluteFill
          style={{
            background: hasDiagram
              ? 'rgba(2,10,17,0.42)'
              : hasClip
                ? 'linear-gradient(90deg, rgba(2,10,17,0.5), transparent 66%), linear-gradient(0deg, rgba(2,10,17,0.64), transparent 46%)'
                : 'transparent',
          }}
        />
        {isTitle && !hasClip ? <TitleMark frame={frame} /> : null}
        {scene.diagram ? <Diagram kind={scene.diagram} frame={frame} durationInFrames={durationInFrames} /> : null}
        {!isTitle && scene.kicker ? <div style={kickerStyle}>{scene.kicker}</div> : null}
        <div style={isTitle ? titleCopyStyle : copyStyle}>
          <div style={{...titleStyle, fontSize: isTitle ? 82 : 56}}>{scene.title}</div>
          {scene.subtitle ? <div style={{...subtitleStyle, fontSize: isTitle ? 38 : 39}}>{scene.subtitle}</div> : null}
        </div>
        <div style={bottomRuleStyle} />
      </AbsoluteFill>
    </AbsoluteFill>
  );
};

const OceanAtmosphere: React.FC<{frame: number; transparent?: boolean}> = ({frame, transparent}) => {
  const t = frame / 30;
  return (
    <AbsoluteFill style={{background: transparent ? 'transparent' : 'radial-gradient(ellipse at 70% 36%, rgba(17,91,111,.26), transparent 44%), linear-gradient(120deg, #06121b, #04101b 55%, #071925)'}}>
      {!transparent ? <svg width="100%" height="100%" viewBox="0 0 1920 1080" style={{position: 'absolute', opacity: 0.4}}>
        {Array.from({length: 44}, (_, i) => {
          const x = (i * 337 + 83) % 1900;
          const baseY = (i * 193 + 53) % 1060;
          const y = baseY + Math.sin(t * 0.3 + i * 1.7) * 9;
          const r = 0.7 + (i % 4) * 0.35;
          const alpha = 0.18 + ((Math.sin(t * 0.9 + i * 2.1) + 1) * 0.2);
          return <circle key={i} cx={x} cy={y} r={r} fill={i % 6 === 0 ? gold : cyan} opacity={alpha} />;
        })}
      </svg> : null}
      {!transparent ? <div style={{position: 'absolute', right: -210, top: 90, width: 520, height: 520, borderRadius: '50%', border: '1px solid rgba(123,231,236,.12)', transform: `scale(${1 + Math.sin(t * 0.25) * 0.035})`}} /> : null}
    </AbsoluteFill>
  );
};

const TitleMark: React.FC<{frame: number}> = ({frame}) => {
  const drift = Math.sin(frame / 52) * 12;
  return (
    <div style={{position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', transform: `translateY(${drift}px)`}}>
      <div style={{width: 190, height: 190, border: '1px solid rgba(123,231,236,.42)', borderRadius: '50%', display: 'grid', placeItems: 'center', boxShadow: '0 0 100px rgba(72,201,213,.12)'}}>
        <div style={{width: 11, height: 11, borderRadius: '50%', background: gold, boxShadow: '0 0 28px rgba(241,201,117,.8)'}} />
      </div>
    </div>
  );
};

const kickerStyle: React.CSSProperties = {
  position: 'absolute', left: 124, top: 92, fontSize: 32, letterSpacing: '0.18em', color: gold,
  textTransform: 'uppercase', opacity: 0.92,
};
const copyStyle: React.CSSProperties = {
  position: 'absolute', left: 120, bottom: 146, maxWidth: 1380,
  textShadow: '0 3px 24px rgba(0,0,0,.75)',
};
const titleCopyStyle: React.CSSProperties = {
  position: 'absolute', left: 0, right: 0, bottom: 174, textAlign: 'center',
  textShadow: '0 3px 28px rgba(0,0,0,.8)',
};
const titleStyle: React.CSSProperties = {fontWeight: 600, lineHeight: 1.24, letterSpacing: '0.035em'};
const subtitleStyle: React.CSSProperties = {marginTop: 17, color: 'rgba(224,248,250,.86)', fontWeight: 400, letterSpacing: '0.07em', lineHeight: 1.45};
const bottomRuleStyle: React.CSSProperties = {position: 'absolute', left: 120, right: 120, bottom: 96, height: 1, background: 'linear-gradient(90deg, rgba(123,231,236,.48), rgba(123,231,236,0))'};

const Diagram: React.FC<{kind: DiagramKind; frame: number; durationInFrames: number}> = ({kind, frame, durationInFrames}) => {
  const enter = interpolate(frame, [0, 26], [0, 1], {...clamp, easing: ease});
  return (
    <div style={{position: 'absolute', left: 270, right: 270, top: 210, height: 520, opacity: enter, transform: `translateY(${(1 - enter) * 22}px)`}}>
      <div style={{position: 'absolute', right: 0, top: -42, zIndex: 2, color: gold, fontSize: 32, letterSpacing: '0.12em'}}>概念図</div>
      {kind === 'particle-choir' ? <ParticleChoir frame={frame} durationInFrames={durationInFrames} /> : null}
      {kind === 'gpu-skeleton' ? <GpuSkeleton frame={frame} durationInFrames={durationInFrames} /> : null}
      {kind === 'rhythm-sync' ? <RhythmSync frame={frame} durationInFrames={durationInFrames} /> : null}
      {kind === 'vr-depth' ? <VrDepth frame={frame} /> : null}
      {kind === 'production-flow' ? <ProductionFlow frame={frame} durationInFrames={durationInFrames} /> : null}
    </div>
  );
};

type Point = {x: number; y: number; phase: number; size: number; tint: number};
const particlePoints: Point[] = Array.from({length: 420}, (_, i) => {
  const u = ((i * 0.61803398875) % 1);
  const v = ((i * 0.7548776662) % 1);
  const centered = u * 2 - 1;
  // Elliptical body with a tapered tail and snout: a symbolic whale silhouette.
  const body = Math.abs(centered) < 0.7 && v < 0.72;
  const x = body ? 255 + u * 840 : 180 + u * 980;
  const yCenter = 254 + Math.sin((x - 255) / 840 * Math.PI) * 65;
  const thickness = body ? 30 + 100 * Math.sqrt(Math.max(0, 1 - centered * centered)) : 14;
  const y = yCenter + (v - 0.5) * thickness;
  return {x, y, phase: (i * 2.399963) % (Math.PI * 2), size: 1.6 + ((i * 17) % 5) * 0.58, tint: i % 9 === 0 ? 1 : 0};
});

const coralDestination = (i: number) => {
  const branch = i % 7;
  const along = (Math.floor(i / 7) * 31 % 60) / 59;
  const direction = branch - 3;
  return {
    x: 700 + direction * 22 + direction * 128 * along + Math.sin(along * 12 + branch) * 7,
    y: 424 - along * 205 + Math.cos(along * 8 + branch) * 5,
  };
};

const ParticleChoir: React.FC<{frame: number; durationInFrames: number}> = ({frame, durationInFrames}) => {
  const phase = interpolate(frame, [durationInFrames * 0.28, durationInFrames * 0.78], [0, 1], {...clamp, easing: ease});
  const scatter = Math.sin(Math.min(1, phase) * Math.PI) * 95;
  const time = frame / 30;
  const whaleOpacity = interpolate(phase, [0, 0.48, 0.78, 1], [0.17, 0.17, 0.08, 0], clamp);
  const coralOpacity = interpolate(phase, [0.48, 0.82, 1], [0, 0.32, 0.72], clamp);
  return (
    <svg width="100%" height="100%" viewBox="0 0 1400 520" role="img" aria-label="Concept particle flow diagram">
      <path d="M160 260 C270 140 650 142 1040 235 C1124 255 1195 248 1250 206 C1228 250 1227 274 1250 313 C1170 284 1117 276 1034 292 C641 371 281 365 160 260Z" fill="none" stroke="rgba(123,231,236,.2)" strokeWidth="1" opacity={whaleOpacity} />
      <g fill="none" stroke={gold} strokeWidth="2.5" strokeLinecap="round" opacity={coralOpacity}>
        <path d="M700 435 C692 380 684 326 666 266 M692 383 C652 354 621 325 608 285 M686 350 C720 318 749 287 757 248" />
        <path d="M570 435 C576 381 562 332 541 289 M576 382 C608 359 626 333 632 306 M825 435 C817 379 831 327 852 286 M817 382 C786 356 775 326 778 296" />
        <path d="M450 435 C462 392 455 355 438 323 M950 435 C938 392 945 355 962 323 M340 435 H1060" />
      </g>
      {particlePoints.map((p, i) => {
        const coral = coralDestination(i);
        const wanderX = Math.sin(p.phase + time * 0.24) * scatter;
        const wanderY = Math.cos(p.phase * 0.73 + time * 0.19) * scatter * 0.48;
        const x = p.x + (coral.x - p.x) * phase + wanderX;
        const y = p.y + (coral.y - p.y) * phase + wanderY + Math.sin(time * 1.1 + p.phase) * 2.5;
        const pulse = 0.3 + (Math.sin(time * 0.8 + p.phase) + 1) * 0.24;
        return <circle key={i} cx={x} cy={y} r={p.size} fill={p.tint ? gold : cyan} opacity={pulse} />;
      })}
      <text x="700" y="472" fill="rgba(217,246,247,.9)" fontSize="32" textAnchor="middle">生物  →  散開  →  海底の群落</text>
      <text x="700" y="510" fill="rgba(241,201,117,.92)" fontSize="30" textAnchor="middle">同じIDを保持</text>
    </svg>
  );
};

const GpuSkeleton: React.FC<{frame: number; durationInFrames: number}> = ({frame, durationInFrames}) => {
  const flow = interpolate(frame, [durationInFrames * 0.18, durationInFrames * 0.77], [0, 1], {...clamp, easing: ease});
  const dots = Array.from({length: 420}, (_, i) => {
    const t = i / 85;
    return {x: 210 + t * 960, y: 250 + Math.sin(t * Math.PI * 1.8) * 52 + Math.sin(t * 8) * 14, r: 1.8 + (i % 5) * 0.38};
  });
  return (
    <svg width="100%" height="100%" viewBox="0 0 1400 520">
      <text x="135" y="96" fill="rgba(224,248,250,.88)" fontSize="32" textAnchor="middle">CPU骨格</text>
      <text x="650" y="96" fill="rgba(224,248,250,.88)" fontSize="32" textAnchor="middle">Compute Shader</text>
      <text x="1110" y="96" fill="rgba(224,248,250,.88)" fontSize="32" textAnchor="middle">URP</text>
      <text x="380" y="96" fill="rgba(123,231,236,.8)" fontSize="34" textAnchor="middle">→</text>
      <text x="880" y="96" fill="rgba(123,231,236,.8)" fontSize="34" textAnchor="middle">→</text>
      <text x="650" y="145" fill="rgba(217,246,247,.82)" fontSize="32" textAnchor="middle">GraphicsBuffer</text>
      <path d="M170 270 C350 130 575 348 820 238 S1085 214 1190 280" fill="none" stroke="rgba(241,201,117,.55)" strokeWidth="4" strokeDasharray="8 11" />
      {Array.from({length: 8}, (_, i) => {
        const x = 210 + i * 133;
        const y = 255 + Math.sin(i * 0.9) * 36;
        return <g key={i}><circle cx={x} cy={y} r={8} fill={gold} /><circle cx={x} cy={y} r={15} fill="none" stroke="rgba(241,201,117,.2)" /></g>;
      })}
      <path d="M170 390 H1215" fill="none" stroke="rgba(123,231,236,.2)" strokeWidth="1" />
      {dots.map((d, i) => <circle key={i} cx={d.x + flow * 12} cy={d.y + Math.sin(frame / 17 + i) * 5} r={d.r} fill={i % 11 === 0 ? gold : cyan} opacity={0.38 + (i % 5) * 0.1} />)}
      <text x="1080" y="438" fill="rgba(217,246,247,.84)" fontSize="32" textAnchor="middle">GPU粒子描画</text>
      <text x="700" y="490" fill="rgba(224,248,250,.74)" fontSize="28" textAnchor="middle">共有骨格を使う表現の概念図</text>
    </svg>
  );
};

const RhythmSync: React.FC<{frame: number; durationInFrames: number}> = ({frame, durationInFrames}) => {
  const beat = (frame / 30) % 1;
  const pulse = 1 - Math.abs(beat - 0.5) * 1.4;
  const progress = interpolate(frame, [0, durationInFrames], [0, 1], clamp);
  const marks = [0.18, 0.5, 0.82];
  const labels = ['DSP拍', '発射', '着弾音'];
  return (
    <svg width="100%" height="100%" viewBox="0 0 1400 520">
      <text x="120" y="95" fill="rgba(224,248,250,.84)" fontSize="32">DSP共通時計で、音と操作を合わせる</text>
      <text x="120" y="158" fill="rgba(241,201,117,.9)" fontSize="32">ロック取得は入力に応じて随時</text>
      <path d="M110 270 H1290" stroke="rgba(123,231,236,.24)" strokeWidth="2" />
      {Array.from({length: 42}, (_, i) => {
        const x = 130 + i * 28;
        const amp = 18 + Math.abs(Math.sin(i * 0.76 + progress * 5)) * (45 + pulse * 22);
        return <line key={i} x1={x} x2={x} y1={270 - amp} y2={270 + amp} stroke={i % 4 === 0 ? gold : cyan} strokeWidth={i % 4 === 0 ? 3 : 2} opacity={0.26 + pulse * 0.54} />;
      })}
      {marks.map((at, i) => {
        const x = 130 + at * 1150;
        return <g key={labels[i]}>
          <line x1={x} x2={x} y1={205} y2={376} stroke="rgba(241,201,117,.5)" strokeDasharray="5 8" />
          <circle cx={x} cy={270} r={18 + (i === 0 ? pulse * 7 : 0)} fill="rgba(241,201,117,.1)" stroke={gold} strokeWidth="2" />
          <text x={x} y={430} textAnchor="middle" fill="#e6f7f8" fontSize="32">{labels[i]}</text>
        </g>;
      })}
      <text x="700" y="490" fill="rgba(217,246,247,.84)" fontSize="30" textAnchor="middle">発射・着弾音を拍へ予約</text>
    </svg>
  );
};

const VrDepth: React.FC<{frame: number}> = ({frame}) => {
  const sway = Math.sin(frame / 30 * 0.42) * 18;
  const layers = [0, 1, 2, 3];
  return (
    <svg width="100%" height="100%" viewBox="0 0 1400 520">
      <text x="110" y="95" fill="rgba(224,248,250,.8)" fontSize="32">手前  /  中景  /  遠景</text>
      {layers.map((layer) => {
        const z = layer / 3;
        const scale = 0.42 + z * 0.56;
        const cx = 700 + sway * (1 - z);
        const cy = 280 + Math.sin(frame / 70 + layer) * (8 + z * 5);
        return <g key={layer} opacity={0.26 + z * 0.22}>
          <ellipse cx={cx} cy={cy} rx={310 * scale} ry={154 * scale} fill="none" stroke={layer === 3 ? gold : cyan} strokeWidth={layer === 3 ? 2.5 : 1.4} />
          {Array.from({length: 7}, (_, i) => <circle key={i} cx={cx + Math.cos(i * 0.9) * 285 * scale} cy={cy + Math.sin(i * 0.9) * 130 * scale} r={3 + z * 2} fill={i === layer ? gold : cyan} />)}
        </g>;
      })}
      <circle cx={700 + sway} cy={280} r={8} fill="#f5feff" opacity={0.9} />
      <text x="700" y="474" fill="rgba(217,246,247,.82)" fontSize="32" textAnchor="middle">視点の移動に応じて変わる奥行き</text>
    </svg>
  );
};

const ProductionFlow: React.FC<{frame: number; durationInFrames: number}> = ({frame, durationInFrames}) => {
  const progress = interpolate(frame, [8, durationInFrames - 8], [0, 1], {...clamp, easing: ease});
  const items = ['人の指示', 'AIが実装', '実画面\n検証', '修正'];
  return (
    <svg width="100%" height="100%" viewBox="0 0 1400 520">
      {items.map((label, i) => {
        const x = 160 + i * 360;
        const active = Math.min(3, Math.floor(progress * 4.3));
        const alpha = i <= active ? 0.9 : 0.38;
        return <g key={label} opacity={alpha}>
          {i < items.length - 1 ? <path d={`M${x + 118} 270 H${x + 330}`} stroke="rgba(123,231,236,.35)" strokeWidth="2" markerEnd="url(#arrow)" /> : null}
          <circle cx={x} cy={270} r={70} fill="rgba(7,27,38,.38)" stroke={i === active ? gold : 'rgba(123,231,236,.48)'} strokeWidth={i === active ? 2.5 : 1.5} />
          <text x={x} y={label.includes('\n') ? 263 : 280} textAnchor="middle" fill="#eefcff" fontSize="32" fontWeight="500">{label.split('\n').map((line, j) => <tspan key={line} x={x} dy={j === 0 ? 0 : 38}>{line}</tspan>)}</text>
          {i === active ? <circle cx={x} cy={270} r={83 + Math.sin(frame / 13) * 4} fill="none" stroke="rgba(241,201,117,.48)" strokeWidth="1" /> : null}
        </g>;
      })}
      <defs><marker id="arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto"><path d="M0,0 L0,6 L9,3 z" fill="rgba(123,231,236,.6)" /></marker></defs>
      <text x="700" y="445" fill="rgba(217,246,247,.84)" fontSize="30" textAnchor="middle">人が方向を決め、AIが実装。画面で確かめ、修正する。</text>
    </svg>
  );
};
