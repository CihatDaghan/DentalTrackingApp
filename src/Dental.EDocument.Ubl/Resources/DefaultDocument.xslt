<?xml version="1.0" encoding="UTF-8"?>
<!-- Varsayılan gömülü görüntüleme şablonu (placeholder).
     Tenant logolu özel şablonlar EDocumentTemplates tablosundan gelecek. -->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                xmlns:inv="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                xmlns:cn="urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2"
                exclude-result-prefixes="inv cn cac cbc">
  <xsl:output method="html" encoding="UTF-8" indent="yes"/>

  <xsl:template match="/">
    <html>
      <head>
        <meta http-equiv="Content-Type" content="text/html; charset=UTF-8"/>
        <title><xsl:value-of select="/*/cbc:ID"/></title>
        <style>
          body { font-family: Arial, sans-serif; font-size: 12px; margin: 24px; }
          table { border-collapse: collapse; width: 100%; margin-top: 12px; }
          th, td { border: 1px solid #999; padding: 4px 8px; text-align: left; }
          .totals { margin-top: 12px; text-align: right; }
        </style>
      </head>
      <body>
        <h2><xsl:value-of select="/*/cac:AccountingSupplierParty/cac:Party/cac:PartyName/cbc:Name"/></h2>
        <p>
          Belge No: <xsl:value-of select="/*/cbc:ID"/><br/>
          ETTN: <xsl:value-of select="/*/cbc:UUID"/><br/>
          Tarih: <xsl:value-of select="/*/cbc:IssueDate"/><br/>
          Senaryo: <xsl:value-of select="/*/cbc:ProfileID"/>
        </p>
        <p>
          Alıcı:
          <xsl:value-of select="/*/cac:AccountingCustomerParty/cac:Party/cac:PartyName/cbc:Name"/>
          <xsl:text> </xsl:text>
          <xsl:value-of select="/*/cac:AccountingCustomerParty/cac:Party/cac:Person/cbc:FirstName"/>
          <xsl:text> </xsl:text>
          <xsl:value-of select="/*/cac:AccountingCustomerParty/cac:Party/cac:Person/cbc:FamilyName"/>
        </p>
        <table>
          <tr><th>Hizmet</th><th>Miktar</th><th>Birim Fiyat</th><th>KDV</th><th>Tutar</th></tr>
          <xsl:for-each select="/*/cac:InvoiceLine | /*/cac:CreditNoteLine">
            <tr>
              <td><xsl:value-of select="cac:Item/cbc:Name"/></td>
              <td><xsl:value-of select="cbc:InvoicedQuantity | cbc:CreditedQuantity"/></td>
              <td><xsl:value-of select="cac:Price/cbc:PriceAmount"/></td>
              <td><xsl:value-of select="cac:TaxTotal/cbc:TaxAmount"/></td>
              <td><xsl:value-of select="cbc:LineExtensionAmount"/></td>
            </tr>
          </xsl:for-each>
        </table>
        <div class="totals">
          <p>
            Toplam: <xsl:value-of select="/*/cac:LegalMonetaryTotal/cbc:LineExtensionAmount"/><br/>
            KDV: <xsl:value-of select="/*/cac:TaxTotal/cbc:TaxAmount"/><br/>
            Ödenecek: <xsl:value-of select="/*/cac:LegalMonetaryTotal/cbc:PayableAmount"/>
          </p>
        </div>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>
