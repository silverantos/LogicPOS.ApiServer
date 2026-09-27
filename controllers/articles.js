module.exports = ({ db }) => ({
  LogicPOSApiFeaturesArticlesArticlesGetAutocompleteLinesGetAutocompleteLinesEndpoint: async (contexto, req, res) => {
    try {
      const artigos = await db.all(`
        SELECT Id, Code, Designation
        FROM "Articles"
        WHERE IsDeleted = 0 OR IsDeleted IS NULL
        ORDER BY "Order", Designation
      `);
      return res.json(artigos.map(artigo => ({
        id: artigo.Id,
        code: String(artigo.Code ?? ''),
        name: artigo.Designation || ''
      })));
    } catch (err) {
      console.error('Erro ao obter as sugestões de artigos:', err);
      return res.status(500).json({ title: 'Internal Server Error', status: 500 });
    }
  }
});