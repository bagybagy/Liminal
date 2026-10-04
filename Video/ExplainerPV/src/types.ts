export type DiagramKind = 'particle-choir' | 'gpu-skeleton' | 'rhythm-sync' | 'vr-depth' | 'production-flow';

export type SceneManifest = {
  id: string;
  durationSeconds: number;
  title: string;
  subtitle?: string;
  kicker?: string;
  clip?: string;
  clipStartSeconds?: number;
  sourceAudio?: boolean;
  diagram?: DiagramKind;
};

export type VideoManifest = {
  title: string;
  creator: string;
  fps: number;
  width: number;
  height: number;
  audioMix?: string;
  scenes: SceneManifest[];
};
