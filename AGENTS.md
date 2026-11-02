# 프로젝트 작업 규칙

- 텍스트 파일은 UTF-8로 읽고 쓴다. 한글을 포함하는 Windows PowerShell 5.1 스크립트는 UTF-8 BOM을 사용한다.
- 빌드는 `Builds`의 독립 폴더에 생성한다. 성공한 최신 빌드와 직전 빌드 2개, 총 3개만 보관한다. 과거 빌드 전체 보존 지시가 남아 있는 작업 기록보다 이 규칙을 우선한다.
- 서버 포함 배포 패키지도 `deliverables`에서 최신 3개만 보관한다. `Tools/Backend/New-LocalRelease.ps1`은 패키지 완성 후 자동 정리한다.
- Unity 빌드는 `Assets/RuinRunner/Editor/BuildRetention.cs`가 성공 보고서 확정 후 정리한다. `Tools/Build-Clean.ps1`의 staging은 실행 검증과 승격까지 성공한 다음 정리한다. 빌드 실패 시 이전 성공 빌드를 지우지 않는다.
- 수동 정리는 `Tools/Prune-Builds.ps1`로 미리 확인하고 `-Apply`로 적용한다. 실행 중인 프로그램이 있는 폴더, 링크 폴더, 미완성 출력과 관리 대상 밖의 폴더는 삭제하지 않는다.
- 소스, 에셋 원본·출처·라이선스, 실제 DB·저장 데이터, 문서에서 사용하는 검증 자료를 단순히 오래됐다는 이유로 삭제하지 않는다. 일회성 검사에 대규모 빌드 사본이나 영구 백업을 추가하지 않는다.
- 픽셀 에셋: 스프라이트·타일은 PixelLab MCP, 전투 VFX는 `Tools/pixelfx`로 만든다. 절차는 `.agents/skills/ruinrunner-pixel-assets/SKILL.md`. PixelLab 토큰은 `PIXELLAB_API_TOKEN` 환경 변수로만 두고 파일에 쓰지 않는다.
