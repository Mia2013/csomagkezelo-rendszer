import { Avatar, Box, CardActionArea, Paper, Stack, Typography } from "@mui/material";

const DashboardCard = ({ icon, title, count, selected, onClick }) => (
  <Paper
    elevation={selected ? 6 : 1}
    sx={{
      borderRadius: 3,
      border: "2px solid",
      borderColor: selected ? "primary.dark" : "transparent",
      bgcolor: selected ? "primary.dark" : "background.paper",
      color: selected ? "common.white" : "text.primary",
      transition: "background-color 200ms, border-color 200ms",
    }}
  >
    <CardActionArea onClick={onClick} sx={{ p: 2 }}>
      <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
        <Avatar sx={{ bgcolor: selected ? "common.white" : "#e3f2fd", color: "primary.dark" }}>
          {icon}
        </Avatar>
        <Box sx={{ flexGrow: 1 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            {title}
          </Typography>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            {count}
          </Typography>
        </Box>
      </Stack>
    </CardActionArea>
  </Paper>
);

export default DashboardCard;
