LogicPOS.ApiServer é um projeto multiplataforma de um servidor para LogicPOS. Para rodar localmente fazer download ZIP e descompactar.
Dentro da pasta extraida rodar "npm install" em linux.
Este comando lê o ficheiro package.json e reinstala todas as dependências automaticamente em poucos segundos, criando a pasta node_modules localmente de forma limpa.
Este projeto não vai criar a base de dados Sqlite, terá que se usar uma existente do LogicPOS cliente dentro da mesma pasta. Copiar também o ficheiro da licença do cliente LogicPOS.
Para executar a apiserver basta executar "node server.js". 
Ajustar o cliente para comunicar em localhost:5001
Este projeto ainda não está completo e muitas correções terão que ser feitas de futuro.
