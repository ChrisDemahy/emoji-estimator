/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#f0f9ff',
          100: '#e0f2fe',
          200: '#bae6fd',
          300: '#7dd3fc',
          400: '#38bdf8',
          500: '#0ea5e9',
          600: '#0284c7',
          700: '#0369a1',
          800: '#075985',
          900: '#0c4a6e',
        },
        accent: {
          50: '#fdf4ff',
          100: '#fae8ff',
          200: '#f5d0fe',
          300: '#f0abfc',
          400: '#e879f9',
          500: '#d946ef',
          600: '#c026d3',
          700: '#a21caf',
          800: '#86198f',
          900: '#701a75',
        }
      },
      boxShadow: {
        glow: "0 24px 80px -40px rgb(15 23 42 / 0.9)",
        'brand-glow': "0 20px 60px -20px rgba(14, 165, 233, 0.3)",
        'accent-glow': "0 20px 60px -20px rgba(217, 70, 239, 0.3)",
      },
      backgroundImage: {
        'gradient-radial': 'radial-gradient(var(--tw-gradient-stops))',
        'gradient-brand': 'linear-gradient(135deg, #0ea5e9 0%, #0369a1 100%)',
        'gradient-accent': 'linear-gradient(135deg, #e879f9 0%, #c026d3 100%)',
      }
    }
  },
  plugins: []
};
