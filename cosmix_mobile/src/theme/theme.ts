import { StyleSheet } from 'react-native';

export const colors = {
  navy: '#102D42',
  teal: '#087E83',
  mint: '#DFF3EE',
  background: '#F3F6F8',
  white: '#FFFFFF',
  text: '#183447',
  muted: '#5D7280',
  line: '#D5E0E5',
  danger: '#A12C36',
  dangerSoft: '#FFF0F0',
  amber: '#815500',
  amberSoft: '#FFF4D9',
};

export const layout = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { padding: 24, gap: 20, flexGrow: 1 },
  card: {
    backgroundColor: colors.white,
    borderRadius: 20,
    padding: 20,
    borderWidth: 1,
    borderColor: colors.line,
    gap: 12,
  },
  title: {
    fontSize: 30,
    fontWeight: '700',
    color: colors.navy,
    letterSpacing: -0.6,
  },
  heading: { fontSize: 20, fontWeight: '700', color: colors.navy },
  body: { fontSize: 15, lineHeight: 23, color: colors.text },
  muted: { fontSize: 14, lineHeight: 21, color: colors.muted },
  label: { fontSize: 14, fontWeight: '600', color: colors.navy },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
  },
  stack6: { gap: 6 },
  stack8: { gap: 8 },
  divider: { height: 1, backgroundColor: colors.line },
});
