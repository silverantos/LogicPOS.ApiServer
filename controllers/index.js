const fs = require('fs');
const path = require('path');

module.exports = (dependencias) => fs.readdirSync(__dirname)
  .filter(ficheiro => ficheiro.endsWith('.js') && ficheiro !== 'index.js')
  .sort()
  .reduce((controladores, ficheiro) => ({
    ...controladores,
    ...require(path.join(__dirname, ficheiro))(dependencias)
  }), {});