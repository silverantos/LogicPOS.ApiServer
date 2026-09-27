module.exports = ({ db }) => ({
  LogicPOSApiFeaturesArticlesStocksWarehouseArticlesGetTotalStocksGetArticlesTotalStocksEndpoint: async (contexto, req, res) => {
    try {
      const pedido = contexto.request.query.articleIds;
      const articleIds = pedido === undefined
        ? (await db.all('SELECT Id FROM "Articles" WHERE IsDeleted = 0 OR IsDeleted IS NULL')).map(artigo => artigo.Id)
        : Array.isArray(pedido) ? pedido : [pedido];
      const linhas = await db.all(`
        SELECT ArticleId, SUM(Quantity) AS Quantity
        FROM "WarehouseArticles"
        WHERE IsDeleted = 0 OR IsDeleted IS NULL
        GROUP BY ArticleId
      `);
      const quantidades = new Map(linhas.map(linha => [linha.ArticleId.toLowerCase(), Number(linha.Quantity)]));
      return res.json(articleIds.map(articleId => ({
        articleId,
        quantity: quantidades.get(articleId.toLowerCase()) || 0
      })));
    } catch (err) {
      console.error('Erro ao calcular o stock total dos artigos:', err);
      return res.status(500).json({ title: 'Internal Server Error', status: 500 });
    }
  }
});