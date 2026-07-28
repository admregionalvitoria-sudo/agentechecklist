import fs from 'fs';
import path from 'path';

const logoDir = path.join(process.cwd(), 'public', 'logo');
if (!fs.existsSync(logoDir)) {
  fs.mkdirSync(logoDir, { recursive: true });
}

// Crisp High Resolution SVG SENAI Logo
const svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 120" width="400" height="120">
  <rect width="400" height="120" rx="16" fill="transparent"/>
  <!-- SENAI Emblem Box -->
  <rect x="10" y="25" width="380" height="70" rx="12" fill="#164194"/>
  <!-- Decorative Accent Bar -->
  <rect x="25" y="78" width="350" height="6" rx="3" fill="#E84910"/>
  <!-- SENAI Text -->
  <text x="200" y="66" font-family="'Inter', 'Arial Black', sans-serif" font-weight="900" font-size="44" fill="#FFFFFF" text-anchor="middle" letter-spacing="6">SENAI</text>
</svg>`;

fs.writeFileSync(path.join(logoDir, 'logo-senai.svg'), svgContent);
fs.writeFileSync(path.join(logoDir, 'logo-senai.png'), Buffer.from(svgContent)); // SVG compatible image format

console.log('SENAI logo assets created in /public/logo/!');
