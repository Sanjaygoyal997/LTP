/*
 * Curing press layout configuration.
 *
 * The plant floor is drawn as a set of BAYS. Each bay holds one or more rows,
 * each row is a list of cells. A cell is either:
 *   "4919"            -> a curing press with that press number
 *   { id, kind }      -> an explicit cell; kind is "press" | "label" | "gap"
 *
 * Edit this file (or replace it with data pulled from the MES) to match the
 * actual shop-floor arrangement. Nothing else in the app hard-codes press
 * numbers.
 */
const PRESS_LAYOUT = {
  /* widest row on the floor; drives tile sizing on the wall display */
  columns: 18,
  bays: [
    {
      name: 'Bay 1',
      rows: [
        ['4919','4918','4917','4916','4911','4910','4901','4909','4904','4908','4907','4902','4906','4905','9413','9414'],
        ['4925','4924','4921','4920','4915','4914','4913','4903','4912','9201','9202','9702','9203','4923','4922',
         { id: 'T 6', kind: 'label' }]
      ]
    },
    {
      name: 'Bay 2',
      rows: [
        ['9401','9402','9403','9404','9405','9406','9804','9803','9802','9407','9408','9505','9506','9507',
         { id: 'T 5', kind: 'label' }],
        ['9409','9410','9411','9412','4416','4417','54801','54802','54803','54804','9502','9503','9504',
         { id: '9802 Ok', kind: 'label' }]
      ]
    },
    {
      name: 'Bay 3',
      rows: [
        ['4409','4408','4407','4406','4405','4404','4403','4402','4801','4802','9701','9703','9704','9002','9001',
         { id: 'TRH', kind: 'label' }],
        ['4415','4414','4413','4410','8702','8701','8606','8605','8604','8603','8602','8601','4401','4412','4411',
         { id: 'T 4', kind: 'label' }]
      ]
    },
    {
      name: 'Bay 4',
      rows: [
        ['24801','24802','24803','24804','24805','24806','24807','24808','24809','24810','24811','24812','24813',
         { id: 'T 2', kind: 'label' },'24814','24815','24816','24817'],
        ['24818','24819','24820','24821','24822','24823','24824','24825','24826',
         { id: 'T 1', kind: 'label' },'14801','14802','14803','14804','14805','14806','14807','14808'],
        ['14809','14810','14811','14812','14813','15201','15202','15203','15204','15205','15206','15207','15208',
         '15209','15210','15211','15212','15213']
      ]
    }
  ]
};

/* Status codes used across the app. Keep in sync with css/styles.css. */
const STATUS = {
  NO_COMM: 'no-comm',   // grey  - PLC not reachable
  RUNNING: 'running',   // green - curing run / pressure ok
  STOPPED: 'stopped',   // yellow- curing stop
  ALARM:   'alarm'      // red   - alarm
};

const LEGEND = [
  { status: STATUS.NO_COMM, label: 'No Communication' },
  { status: STATUS.RUNNING, label: 'Curing Run / Pressure Ok' },
  { status: STATUS.STOPPED, label: 'Curing Stop' },
  { status: STATUS.ALARM,   label: 'Alarm' }
];
