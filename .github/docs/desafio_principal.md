
# Descrição

Faça duas aplicações com .NET 10 SDK:

- **OrderGenerator** em C# com frontend de livre escolha.
- **OrderAccumulator** em C#.

As aplicações se comunicam usando o protocolo FIX com a versão 4.4 (usar a lib do QuickFix disponível em [https://quickfixn.org/](https://quickfixn.org/)).

## OrderGenerator

O OrderGenerator deve apresentar numa página web um formulário com os seguintes campos, com os quais será criada uma nova ordem (`NewOrderSingle`):

- **Símbolo:** escolhido entre PETR4, VALE3 ou VIIA4.
- **Lado:** escolhido entre Compra ou Venda.
- **Quantidade:** valor positivo inteiro menor que 100.000.
- **Preço:** valor positivo decimal múltiplo de 0.01 e menor que 1.000.

No resultado do formulário, apresentar a resposta da requisição.

## OrderAccumulator

O OrderAccumulator recebe as ordens e calcula a exposição financeira por símbolo:

**Exposição financeira =** somatório de (preço × quantidade executada) de cada ordem de compra - somatório de (preço × quantidade executada) de venda.

Ou seja, as ordens de compra aumentam a exposição e as de venda diminuem a exposição.

O OrderAccumulator terá um limite interno constante, por símbolo, de R$ 100.000.000 (cem milhões).

Isso significa que qualquer ordem que venha a ultrapassar em valor absoluto o limite de exposição deve ser respondida com uma rejeição.

Ou seja, caso a ordem seja aceita, o OrderAccumulator deve responder com um `ExecutionReport` tendo `ExecType = New` e a ordem deve ser considerada no cálculo de exposição.

Caso a ordem seja rejeitada, o OrderAccumulator deve responder com um `ExecutionReport` tendo `ExecType = Rejected` e a ordem não deve ser considerada no cálculo de exposição.

## Readme do Repositório

- Deve conter o título do projeto.
- Uma descrição sobre o projeto em frase.
- Deve conter uma lista com linguagem, framework e/ou tecnologias usadas.
- Como instalar e usar o projeto (instruções).
- Não esqueça o [.gitignore](https://www.toptal.com/developers/gitignore).
- Se está usando GitHub pessoal, referencie que é um challenge by Coodesh:

  > This is a challenge by [Coodesh](https://coodesh.com/).
