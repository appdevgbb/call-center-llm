param aiResourceName string
param location string = resourceGroup().location
param tags object = {}
param sku string = 'S0'

resource cognitiveServices 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: aiResourceName
  location: location
  sku: {
    name: sku // or another SKU as per your requirements
  }
  kind: 'SpeechServices'
  properties: {
    // Properties as required
  }
  tags: tags
}
