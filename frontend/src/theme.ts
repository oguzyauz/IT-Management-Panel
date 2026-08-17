import { createTheme } from '@mui/material/styles';

/**
 * Tek tasarım kaynağı. Figma dosyası paylaşıldığında (şu an parola korumalı, bkz.
 * docs/open-questions.md G1) renk/tipografi değişiklikleri yalnızca bu dosyada yapılır.
 */
export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#1B4965', light: '#5FA8D3', dark: '#0F2E42' },
    secondary: { main: '#62B6CB' },
    success: { main: '#2E7D32' },
    warning: { main: '#ED6C02' },
    error: { main: '#C62828' },
    info: { main: '#0288D1' },
    background: { default: '#F4F6F8', paper: '#FFFFFF' },
    text: { primary: '#1A2027', secondary: '#5A6872' },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
    h1: { fontSize: '1.75rem', fontWeight: 600 },
    h2: { fontSize: '1.4rem', fontWeight: 600 },
    h3: { fontSize: '1.15rem', fontWeight: 600 },
    subtitle2: { fontWeight: 600 },
    button: { textTransform: 'none', fontWeight: 600 },
  },
  components: {
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
    },
    MuiCard: {
      defaultProps: { elevation: 0 },
      styleOverrides: {
        root: { border: '1px solid', borderColor: '#E3E8EE' },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: { fontWeight: 600, backgroundColor: '#F8FAFC' },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { fontWeight: 600 },
      },
    },
  },
});

export const workModeColors: Record<string, string> = {
  Office: '#1B4965',
  HomeOffice: '#62B6CB',
  Leave: '#9E9E9E',
};
