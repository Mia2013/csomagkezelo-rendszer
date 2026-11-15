import { Box, Container, Grid } from "@mui/material";

import PageHeader from "./PageHeader";
import DashboardCard from "./DashboardCard";

const Dashboard = ({ title, icon, cards, selectedId, onSelect, children }) => (
  <Box>
    <PageHeader icon={icon} title={title} />
    <Container maxWidth="lg" sx={{ py: 5 }}>
      <Grid container spacing={2}>
        {cards.map((card) => (
          <Grid key={card.id} size={{ xs: 12, sm: 4 }}>
            <DashboardCard
              icon={card.icon}
              title={card.title}
              count={card.count}
              selected={selectedId === card.id}
              onClick={() => onSelect(card.id)}
            />
          </Grid>
        ))}
      </Grid>

      <Box sx={{ mt: 4 }}>{children}</Box>
    </Container>
  </Box>
);

export default Dashboard;
