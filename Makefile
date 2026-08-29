MCU        = atmega328p
F_CPU      = 16000000UL
PROGRAMMER = arduino
PORT       = COM3
BAUD       = 57600   # old Nano bootloader = 57600, new (UNO) bootloader = 115200

CC      = avr-gcc
OBJCOPY = avr-objcopy
AVRDUDE = avrdude

SRC_DIR   = source
LIB_DIR   = libraries
BUILD_DIR = build

SRCS   = $(SRC_DIR)/main.c
TARGET = $(BUILD_DIR)/main

CFLAGS = -mmcu=$(MCU) -DF_CPU=$(F_CPU) -Os -Wall -std=gnu11 \
         -I$(SRC_DIR) -I$(LIB_DIR) -I$(LIB_DIR)/drivers

.PHONY: all flash clean

all: $(TARGET).hex

$(BUILD_DIR):
	mkdir -p $(BUILD_DIR)

$(TARGET).elf: $(SRCS) | $(BUILD_DIR)
	$(CC) $(CFLAGS) -o $@ $(SRCS)

$(TARGET).hex: $(TARGET).elf
	$(OBJCOPY) -O ihex -R .eeprom $< $@

flash: $(TARGET).hex
	$(AVRDUDE) -c $(PROGRAMMER) -p $(MCU) -P $(PORT) -b $(BAUD) -U flash:w:$<:i

clean:
	rm -rf $(BUILD_DIR)
