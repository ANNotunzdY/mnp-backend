# MacNicoPlayer（Re:仮）バックエンドサーバ

## 動作要件
- .NET 8.0
- AWS Lambda
- DynamoDB
- CloudSearch

## デプロイコマンド
```
dotnet lambda deploy-serverless
```

## CloudSearchの更新
以下のLambdaをEventBridge Schedulerで定期的に呼んでいます
https://github.com/ANNotunzdY/mnp-updater/
