/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    extend: {
      boxShadow: {
        glow: "0 24px 80px -40px rgb(15 23 42 / 0.9)"
      }
    }
  },
  plugins: []
};
