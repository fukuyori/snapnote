$ErrorActionPreference = 'Stop'

# Compile the actual formatter without Windows OCR or third-party test dependencies.
Add-Type -Path (Join-Path $PSScriptRoot '..\Services\OcrTextFormatter.cs')

$cases = @(
    @{
        Name = 'Reported Japanese text and wrapped lines'
        Language = 'ja-JP'
        Lines = @(
            '2026 年 6 月 に 、 オ ル タ ナ テ イ プ な web プ ラ ウ',
            'ザ 実 装 「 Ladybird 」 が 、 PR を 今 後 は 受 け 付 け な',
            'いと い う 方 針 を 発 表 し ま し た 注 1 。 そ の 理 由 は 、'
        )
        Expected = '2026年6月に、オルタナテイプなwebプラウザ実装「Ladybird」が、PRを今後は受け付けないという方針を発表しました注1。その理由は、'
    },
    @{
        Name = 'English words within Japanese text'
        Language = 'ja'
        Lines = @('日 本 語 と OpenAI API を', '使 う')
        Expected = '日本語とOpenAI APIを使う'
    },
    @{
        Name = 'English wrapped lines under Japanese recognition'
        Language = 'JA-jp'
        Lines = @('Hello world', 'from SnapNote Studio 4.2.0')
        Expected = 'Hello world from SnapNote Studio 4.2.0'
    },
    @{
        Name = 'English recognition is unchanged'
        Language = 'en-US'
        Lines = @('日 本 語 and English', 'next line')
        Expected = '日 本 語 and English next line'
    },
    @{
        Name = 'Korean recognition is unchanged'
        Language = 'ko-KR'
        Lines = @('한국어 문장', '다음 줄')
        Expected = '한국어 문장 다음 줄'
    },
    @{
        Name = 'Preserve intentional spacing and paragraph boundaries'
        Language = 'ja-JP'
        Lines = @("姓　名  項 目`t次 の 項 目`n`n別 の 段 落")
        Expected = "姓　名  項目`t次の項目`n`n別の段落"
    },
    @{
        Name = 'Preserve list marker separators'
        Language = 'ja-JP'
        Lines = @("- 日 本 語`n• 次 の 項 目`n1. 番 号 付 き`n(a) 英 字")
        Expected = "- 日本語`n• 次の項目`n1. 番号付き`n(a) 英字"
    },
    @{
        Name = 'Japanese punctuation and brackets'
        Language = 'ja-JP'
        Lines = @('「 OpenAI API 」 （ 日 本 語 ） 。 次 へ ！')
        Expected = '「OpenAI API」（日本語）。次へ！'
    },
    @{
        Name = 'Empty result'
        Language = 'ja-JP'
        Lines = @()
        Expected = ''
    }
)

foreach ($case in $cases) {
    $actual = [SnapNoteStudio.Services.OcrTextFormatter]::JoinLines([string[]]$case.Lines, $case.Language)
    if ($actual -cne $case.Expected) {
        throw "$($case.Name): expected <$($case.Expected)> but got <$actual>"
    }
}
Write-Output "All $($cases.Count) OCR text formatting checks passed."
