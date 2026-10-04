import React from 'react';
import {Composition} from 'remotion';
import manifest from '../public/video-manifest.json';
import {ExplainerVideo} from './video';
import type {VideoManifest} from './types';

const data = manifest as VideoManifest;
const durationInFrames = data.scenes.reduce((total, scene) => total + scene.durationSeconds, 0) * data.fps;

export const RemotionRoot: React.FC = () => (
  <Composition
    id="ExplainerPV"
    component={ExplainerVideo}
    durationInFrames={durationInFrames}
    fps={data.fps}
    width={data.width}
    height={data.height}
    defaultProps={{manifest: data}}
  />
);
