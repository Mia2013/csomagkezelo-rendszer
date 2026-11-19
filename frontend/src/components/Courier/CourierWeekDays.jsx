import { Button, ButtonGroup } from "@mui/material";

import { WEEKDAYS } from "../../constants/constants";
import dayjs from "../../utils/dayjs";

const CourierWeekDays = ({ tasks, selectedDay, onSelect }) => {
  const monday = dayjs(selectedDay).subtract((dayjs(selectedDay).day() + 6) % 7, "day");

  const weekDays = [];
  for (let i = 0; i < 7; i++) {
    weekDays.push(monday.add(i, "day"));
  }

  const getTaskCount = (day) => {
    return tasks.filter((task) => dayjs(task.pickupDate).isSame(day, "day")).length;
  };

  return (
    <ButtonGroup fullWidth size="small" sx={{ mb: 3, flexWrap: "wrap" }}>
      {weekDays.map((day) => {
        const value = day.format("YYYY-MM-DD");
        const count = getTaskCount(day);
        return (
          <Button
            key={value}
            variant={value === selectedDay ? "contained" : "outlined"}
            disabled={count === 0}
            onClick={() => onSelect(value)}
          >
            {day.format("MM.DD.")} {WEEKDAYS[day.day()]} ({count})
          </Button>
        );
      })}
    </ButtonGroup>
  );
};

export default CourierWeekDays;
