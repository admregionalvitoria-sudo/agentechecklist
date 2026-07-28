/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
    "./demo.tsx",
  ],
  theme: {
    extend: {
      colors: {
        senai: {
          blue: "#164194",
          orange: "#E84910",
          cyan: "#43BDD9",
          purple: "#4C3189",
          yellow: "#F3AB15",
          dark: "#0B1936",
        },
      },
      fontFamily: {
        sans: ['Inter', 'Outfit', 'Segoe UI', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        'glass': '0 20px 40px -15px rgba(22, 65, 148, 0.07)',
        'glass-hover': '0 25px 50px -12px rgba(22, 65, 148, 0.12)',
        'glow-sim': '0 0 25px -5px rgba(16, 185, 129, 0.4)',
        'glow-nao': '0 0 25px -5px rgba(239, 68, 68, 0.4)',
        'glow-senai': '0 0 30px -5px rgba(22, 65, 148, 0.3)',
      },
      animation: {
        'pulse-glow': 'pulse-glow 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'float': 'float 6s ease-in-out infinite',
      },
      keyframes: {
        'pulse-glow': {
          '0%, 100%': { opacity: 1 },
          '50%': { opacity: 0.6 },
        },
        'float': {
          '0%, 100%': { transform: 'translateY(0px)' },
          '50%': { transform: 'translateY(-8px)' },
        },
      },
    },
  },
  plugins: [],
}
