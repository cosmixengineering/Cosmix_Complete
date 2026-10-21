import { StyleSheet } from 'react-native';

export const authColors = {
  background: '#ECEEF8',
  card: '#FFFFFF',
  primary: '#39327C',
  ink: '#25234B',
  muted: '#73718C',
  line: '#E8E7F0',
  soft: '#F5F3FC',
  green: '#087C4A',
};

export const authStyles = StyleSheet.create({
  stack: { gap: 10 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  title: {
    color: authColors.ink,
    fontSize: 28,
    lineHeight: 35,
    fontWeight: '700',
    letterSpacing: -0.6,
  },
  heading: {
    color: authColors.ink,
    fontSize: 24,
    lineHeight: 31,
    fontWeight: '700',
    letterSpacing: -0.4,
  },
  body: { color: authColors.muted, fontSize: 14, lineHeight: 22 },
  label: { color: authColors.ink, fontSize: 14, fontWeight: '600' },
  caption: { color: authColors.muted, fontSize: 12, lineHeight: 19 },
  centered: { textAlign: 'center' },
  link: { color: authColors.primary, fontWeight: '700', fontSize: 14 },
});
