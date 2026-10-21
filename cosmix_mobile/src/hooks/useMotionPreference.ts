import { useEffect, useState } from 'react';
import { AccessibilityInfo } from 'react-native';

export function useMotionPreference(): boolean {
  const [reduced, setReduced] = useState(true);
  useEffect(() => {
    let active = true;
    let changed = false;
    const subscription = AccessibilityInfo.addEventListener(
      'reduceMotionChanged',
      value => {
        changed = true;
        if (active) setReduced(value);
      },
    );
    AccessibilityInfo.isReduceMotionEnabled()
      .then(value => {
        if (active && !changed) setReduced(value);
      })
      .catch(() => {
        /* Keep motion disabled if the preference is unavailable. */
      });
    return () => {
      active = false;
      subscription.remove();
    };
  }, []);
  return reduced;
}
