using Eticadata.Cust.WebServices.Helpers;
using Eticadata.Cust.WebServices.Models;
using Eticadata.ERP;
using Eticadata.ERP.EtiEnums;
using Eticadata.Views.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Eticadata.Cust.WebServices.Controllers
{
    public class PurchasesController : ApiController
    {
        [HttpPost]
        [Authorize]
        //api/Purchases/GeneratePurchaseDoc
        //data: {
        //"FiscalYear":"2018",
        //"SectionCode":"1",
        //"DocTypeAbbrev":"V/FAT",
        //"EntityCode":"502395028",
        //"Date":"2018/01/25",
        //"ExpirationDate":"2018/02/24",
        //"CurrencyCode":"EUR",
        //"Lines":[
        //	{
        //		"LineNumber": "1",
        //		"ItemCode": "AMORT",
        //		"ItemDescription": "AMORTECEDOR 001",
        //      "BatchCode" : "LOTE_1",
        //      "BatchDescription" : "Lote 0001".
        //      "EntryDate" : "2018-03-27"
        //      "ExpirationDate" : "2019-03-27"
        //		"Quantity": "2",
        //		"VATTax": "23",
        //		"UnitPriceExcludedVAT": "39.90",
        //		"Discount1": "5",
        //		"Discount2": "0",
        //		"Discount3": "0",
        //		"DiscountValue": "0"
        //  }
        //]}
        public IHttpActionResult GeneratePurchaseDoc([FromBody] Models.myPurchase document)
        {
            byte[] reportBytes;

            MovCompra movCompra;
            MovCompraLin movCompraLin;
            var byRefFalse = false;
            var stockAvailable = true;
            var affectsOtherLines = true;
            var fixedAssociation = true;
            var freeAssociation = true;
            var checkStock = false;
            TpProcuraArtigo searchItem = TpProcuraArtigo.NaoEncontrou;
            int numberLine;
            string itemCode;

            try
            {
                movCompra = Eti.Aplicacao.Movimentos.MovCompras.GetNew(document.DocTypeAbbrev, document.SectionCode);
                movCompra.Cabecalho.CodExercicio = document.FiscalYear;

                movCompra.Cabecalho.AplicacaoOrigem = "WS";

                movCompra.Cabecalho.Data = document.Date.Date;
                movCompra.Cabecalho.DataVencimento = document.ExpirationDate;

                movCompra.Cabecalho.CodEntidade = document.EntityCode;
                movCompra.AlteraEntidade(TpEntidade.Fornecedor, movCompra.Cabecalho.CodEntidade, true, true);

                movCompra.Cabecalho.AbrevMoeda = document.CurrencyCode;
                movCompra.AlteraMoeda(document.CurrencyCode, ref byRefFalse, false);

                foreach (PurchaseLine line in document.Lines.OrderBy(p => p.LineNumber))
                {
                    itemCode = line.ItemCode;

                    numberLine = movCompra.Lines.Count + 1;
                    movCompra.AddLin(ref numberLine);
                    movCompraLin = movCompra.Lines[numberLine];
                    movCompraLin.TipoLinha = TpLinha.Artigo;

                    movCompraLin.CodArtigo = itemCode;
                    movCompra.AlteraArtigo(numberLine, ref itemCode, ref affectsOtherLines, ref fixedAssociation, ref freeAssociation, ref searchItem, checkStock, ref stockAvailable);
                    movCompraLin.DescArtigo = line.ItemDescription;

                    //Cria lote caso não exista
                    var item = Eti.Aplicacao.Tabelas.Artigos.Find(itemCode);
                    if (item.Lotes)
                    {
                        var inactive = false;
                        var exists = false;

                        exists = Eti.Aplicacao.Tabelas.Artigos.ExisteLote(itemCode, line.BatchCode, ref inactive);

                        if (!exists)
                        {                            
                            Eti.Aplicacao.Tabelas.Artigos.GravaLote(itemCode, line.BatchCode, line.BatchDescription, line.EntryDate , line.ExpirationDate, "", "", "", "", "");

                            movCompraLin.Lote = line.BatchCode;

                            checkStock = true;
                            stockAvailable = false;

                            //exists : devolve se o lote existe ou não
                            //inactive: devolve se está inativo
                            //se checkStock = true devolve no stockAvailable se existe stock disponivel
                            movCompra.AlteraLote(numberLine, itemCode, line.BatchCode, ref exists, ref inactive, checkStock, ref stockAvailable);
                        }
                    }


                    movCompraLin.Quantidade = line.Quantity;
                    movCompra.AlteraQuantidade(numberLine, movCompraLin.Quantidade, ref affectsOtherLines, false, ref stockAvailable);

                    movCompraLin.PrecoUnitario = Convert.ToDouble(line.UnitPriceExcludedVAT);
                    movCompra.AlteraPrecoUnitario(numberLine, movCompraLin.PrecoUnitario);

                    movCompraLin.TaxaIva = Convert.ToDouble(line.VATTax);
                    movCompraLin.CodTaxaIva = Eti.Aplicacao.Tabelas.TaxasIvas.GetTaxaIva(movCompraLin.TaxaIva);
                    movCompra.AlteraTaxaIVA(numberLine, movCompraLin.CodTaxaIva);

                    movCompraLin.Desconto1 = line.Discount1;
                    movCompra.AlteraDesconto(DiscountTypes.Discount1, numberLine, movCompraLin.Desconto1);

                    movCompraLin.Desconto2 = line.Discount2;
                    movCompra.AlteraDesconto(DiscountTypes.Discount2, numberLine, movCompraLin.Desconto2);

                    movCompraLin.Desconto3 = line.Discount3;
                    movCompra.AlteraDesconto(DiscountTypes.Discount3, numberLine, movCompraLin.Desconto3);

                    movCompraLin.DescontoValorLinha = line.DiscountValue;
                    movCompra.AlteraDesconto(DiscountTypes.DiscountValue, numberLine, movCompraLin.DescontoValorLinha);
                }

                var validate = movCompra.Validate(true);
                if (validate)
                {
                    var blockingStock = false;
                    Eti.Aplicacao.Movimentos.MovCompras.Update(ref movCompra, ref blockingStock, true, 0, "");
                }

                if (!string.IsNullOrEmpty(movCompra.EtiErrorCode))
                {
                    throw new Exception($@"ErrorCode:{movCompra.EtiErrorCode}{Environment.NewLine} EtiErrorDescription:{movCompra.EtiErrorDescription}");
                }
                else
                {
                    DocumentKey docKey = new Helpers.DocumentKey()
                    {
                        SectionCode = movCompra.Cabecalho.CodSeccao,
                        DocTypeAbbrev = movCompra.Cabecalho.AbrevTpDoc,
                        FiscalYear = movCompra.Cabecalho.CodExercicio,
                        Number = movCompra.Cabecalho.Numero
                    };

                    reportBytes = Functions.GetReportBytes(TpDocumentoAEmitir.Compras, docKey);
                }

                return Ok(reportBytes);

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Authorize]
        //GET api/Invoices/PrintPurchaseDoc?fiscalYear=2018&section=1&docType=FAT&number=1
        public IHttpActionResult PrintPurchaseDoc([FromUri] string fiscalYear, [FromUri] string section, [FromUri] string docType, [FromUri] int number)
        {
            try
            {
                DocumentKey docKey = new Helpers.DocumentKey()
                {
                    SectionCode = section,
                    DocTypeAbbrev = docType,
                    FiscalYear = fiscalYear,
                    Number = number,
                };

                byte[] reportBytes = Functions.GetReportBytes(TpDocumentoAEmitir.Compras, docKey);

                return Ok(reportBytes);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }
}
