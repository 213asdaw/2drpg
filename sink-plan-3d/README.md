# 싱크플랜 (SinkPlan)

싱크대 **2D 도면 → 3D 미리보기** 데스크톱 앱입니다.

## Windows에서 그냥 열기 (권장)

1. `SinkPlan-1.0.0-portable.exe` 를 받습니다. (설치 불필요)
2. **더블클릭**하면 바로 실행됩니다.

또는 `SinkPlan-1.0.0-win.zip` 압축을 풀고 안의 `싱크플랜.exe` / `SinkPlan.exe` 를 실행하세요.

## Linux

- `SinkPlan-1.0.0-linux.AppImage` 에 실행 권한을 주고 실행  
  `chmod +x SinkPlan-1.0.0-linux.AppImage && ./SinkPlan-1.0.0-linux.AppImage`
- 또는 `SinkPlan-1.0.0-linux.zip` 압축 해제 후 `SinkPlan` / `sink-plan-3d` 실행

## 개발자용 (소스에서 실행)

```bash
cd sink-plan-3d
npm install
npm run electron:dev   # 데스크톱 창으로 실행
# 또는
npm run dev            # 브라우저 http://localhost:5173
```

설치 파일 다시 만들기:

```bash
npm run dist:win    # Windows portable.exe + zip
npm run dist        # Linux AppImage + zip
```

결과물은 `release-build/` 폴더에 생성됩니다.

## 기능

- 템플릿: 일자형 싱글 / 더블, ㄱ자형, 아일랜드
- 치수(mm) · 2D에서 볼 드래그 · 도면 이미지 오버레이
- 재질 선택 · Orbit 카메라 · 위에서 보기 · PNG 저장
