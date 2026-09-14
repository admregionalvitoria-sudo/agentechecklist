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
          blue: "#1A4B9F",   // Cor principal
          cyan: "#0091D6",   // Cor de apoio
          orange: "#EF5E31", // Cor destaque / secundária
          white: "#FFFFFF",  // Cor de fundo principal
          dark: "#0C1B33",
        },
      },
      fontFamily: {
        sans: ['Inter', 'Montserrat', 'Roboto', 'Neo Sans', 'Segoe UI', 'sans-serif'],
        montserrat: ['Montserrat', 'sans-serif'],
        inter: ['Inter', 'sans-serif'],
        roboto: ['Roboto', 'sans-serif'],
      },
      boxShadow: {
        'glass': '0 20px 40px -15px rgba(26, 75, 159, 0.1)',
        'glass-hover': '0 25px 50px -12px rgba(26, 75, 159, 0.18)',
        'glow-cyan': '0 0 25px -5px rgba(0, 145, 214, 0.45)',
        'glow-orange': '0 0 25px -5px rgba(239, 94, 49, 0.45)',
        'glow-blue': '0 0 30px -5px rgba(26, 75, 159, 0.35)',
        'liquid-card': '0 15px 35px -5px rgba(26, 75, 159, 0.08), 0 0 15px 0 rgba(255, 255, 255, 0.8) inset',
      },
      animation: {
        'pulse-glow': 'pulse-glow 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'float': 'float 7s ease-in-out infinite',
        'liquid-shine': 'shine 2s linear infinite',
      },
      keyframes: {
        'pulse-glow': {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.6' },
        },
        'float': {
          '0%, 100%': { transform: 'translateY(0px) rotate(0deg)' },
          '50%': { transform: 'translateY(-12px) rotate(2deg)' },
        },
      },
    },
  },
  plugins: [],
}
