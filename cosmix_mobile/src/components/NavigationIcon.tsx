import React from 'react';
import Svg, { Circle, Path, Rect } from 'react-native-svg';

export type NavigationIconName =
  | 'home'
  | 'services'
  | 'attendance'
  | 'history'
  | 'profile';

export function NavigationIcon({
  name,
  size = 23,
  color = '#20232B',
}: {
  name: NavigationIconName;
  size?: number;
  color?: string;
}): React.JSX.Element {
  return (
    <Svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth={1.65}
      strokeLinecap="round"
      strokeLinejoin="round"
      accessible={false}
    >
      {name === 'home' ? (
        <Path d="M3 10.5 10.4 4a2.4 2.4 0 0 1 3.2 0l7.4 6.5v8A2.5 2.5 0 0 1 18.5 21h-13A2.5 2.5 0 0 1 3 18.5v-8ZM12 15v3" />
      ) : null}
      {name === 'services' ? (
        <>
          <Rect x={3} y={3} width={7} height={7} rx={2} />
          <Rect x={14} y={3} width={7} height={7} rx={2} />
          <Rect x={3} y={14} width={7} height={7} rx={2} />
          <Rect x={14} y={14} width={7} height={7} rx={2} />
        </>
      ) : null}
      {name === 'attendance' ? (
        <>
          <Path d="M8 3H5a2 2 0 0 0-2 2v3m13-5h3a2 2 0 0 1 2 2v3M3 16v3a2 2 0 0 0 2 2h3m8 0h3a2 2 0 0 0 2-2v-3" />
          <Circle cx={12} cy={9} r={3} />
          <Path d="M7 17a5 5 0 0 1 10 0" />
        </>
      ) : null}
      {name === 'history' ? (
        <>
          <Rect x={4} y={5} width={16} height={16} rx={3} />
          <Path d="M8 3v4m8-4v4M4 10h16m-11 4h3m-3 3h6" />
        </>
      ) : null}
      {name === 'profile' ? (
        <>
          <Circle cx={12} cy={12} r={9} />
          <Circle cx={12} cy={9} r={3} />
          <Path d="M5.5 18a7.2 7.2 0 0 1 13 0" />
        </>
      ) : null}
    </Svg>
  );
}
