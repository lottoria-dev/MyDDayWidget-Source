from PIL import Image, ImageDraw, ImageFont
import os

def create_card_image(text, size, bg_color, text_color, radius=40):
    """단일 카드 이미지를 생성하는 함수"""
    # 투명 배경의 카드 이미지 생성
    card = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(card)
    
    # 둥근 모서리 사각형 그리기
    # 좌표: (0, 0)에서 (width-1, height-1)까지
    draw.rounded_rectangle([(0, 0), (size[0]-1, size[1]-1)], radius=radius, fill=bg_color)
    
    # 텍스트(D) 그리기
    try:
        # 카드 높이의 약 70% 크기로 폰트 설정 (꽉 차게)
        font_size = int(size[1] * 0.7)
        font = ImageFont.truetype("malgunbd.ttf", font_size)
    except IOError:
        # 폰트가 없으면 기본 폰트 사용
        font = ImageFont.load_default()
    
    # 텍스트 크기 계산 및 중앙 정렬
    left, top, right, bottom = font.getbbox(text)
    text_width = right - left
    text_height = bottom - top
    
    # 카드 중앙에 위치 (y축은 시각적 보정을 위해 살짝 위로 올림)
    x = (size[0] - text_width) / 2 - left
    y = (size[1] - text_height) / 2 - top - (size[1] * 0.08)

    draw.text((x, y), text, font=font, fill=text_color)
    return card

def create_dday_icon():
    # 1. 전체 캔버스 설정 (256x256 고해상도)
    canvas_size = (256, 256)
    final_image = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    
    # 카드 크기 설정 (약간 세로로 긴 형태)
    card_w, card_h = 130, 180
    
    # 2. 뒷면 카드 (Back Layer) 생성
    # 깊이감을 위해 메인 컬러보다 약간 어두운 톤 사용
    back_color = "#d64545" 
    back_card = create_card_image("D", (card_w, card_h), back_color, "#e0e0e0")
    
    # 회전 (왼쪽으로 15도 기울임)
    # expand=True: 회전 시 이미지가 잘리지 않도록 캔버스 확장
    back_card_rot = back_card.rotate(15, expand=True, resample=Image.BICUBIC)
    
    # 3. 앞면 카드 (Front Layer) 생성
    # 메인 포인트 컬러 (밝은 붉은색)
    front_color = "#ff6b6b"
    front_card = create_card_image("D", (card_w, card_h), front_color, "white")
    
    # 회전 (오른쪽으로 5도 기울임 - 서로 엇갈리게)
    front_card_rot = front_card.rotate(-5, expand=True, resample=Image.BICUBIC)
    
    # 4. 배치 (Compositing)
    # 뒷면 카드를 먼저 그립니다. (좌측 상단 쪽에 배치)
    # paste(이미지, 좌표, 마스크) - 마스크를 자기 자신으로 써야 투명 배경이 유지됨
    final_image.paste(back_card_rot, (15, 25), back_card_rot)
    
    # 앞면 카드를 그 위에 덮습니다. (우측 하단 쪽에 배치)
    final_image.paste(front_card_rot, (85, 35), front_card_rot)

    # 5. 파일 저장
    # 윈도우용 아이콘(.ico) - 다양한 크기 포함
    final_image.save("icon.ico", format='ICO', sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
    # 트레이/UI용 이미지(.png)
    final_image.save("icon.png", format='PNG')
    
    print(f"아이콘 생성 완료: {os.getcwd()}\\icon.ico, icon.png")

if __name__ == "__main__":
    create_dday_icon()